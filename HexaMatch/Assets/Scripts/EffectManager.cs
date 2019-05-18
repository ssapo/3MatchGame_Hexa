using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EffectManager : MonoBehaviour
{
	public delegate void IntVoid(int integer);
	public event IntVoid OnPointPopupEffectFinished;

	public Material validSelectionMaterial;
	public Material invalidSelectionMaterial;

	public Color nonHighlightedElementColor;
	public Color defaultElementColor;

	public Transform pointPopupDestination;

	public string selectionEffectPoolTag;
	public string pointPopupPoolTag;

	public bool displayConnectedMatchesHighlight = true;

	private PoolManager poolManager;
	private HexGrid grid;
	private LineRenderer selectionLine;

	private List<Transform> activePointPopups;
	private List<SelectionEffectInfo> selectionEffectInfos;
	private Queue<float> collectionEffectSpawnTimes;
	private Queue<GameObject> activeCollectionEffects;

	private float selectionEffectZPos = 0.1f;
	private float selectionLineZPos = -1f;
	private float collectionEffectZPos = -0.1f;
	private float collectionEffectLifetime = 1f;
	private float pointPopupMovementSpeed = 800f;

	private void Start()
	{
		poolManager = GetComponent<PoolManager>();
		grid = GetComponent<HexGrid>();
		selectionLine = GetComponent<LineRenderer>();

		activePointPopups = new List<Transform>();
		selectionEffectInfos = new List<SelectionEffectInfo>();
		collectionEffectSpawnTimes = new Queue<float>();
		activeCollectionEffects = new Queue<GameObject>();

		ClearSelectionLine();
	}

	private void Update()
	{
		if (collectionEffectSpawnTimes.Count > 0)
		{
			//int effectsDisabled = 0;
			while (collectionEffectSpawnTimes.Count > 0 && Time.time >= collectionEffectSpawnTimes.Peek() + collectionEffectLifetime)
			{
				collectionEffectSpawnTimes.Dequeue();
				activeCollectionEffects.Dequeue().SetActive(false);
				//effectsDisabled++;
			}

			//if (effectsDisabled > 0)
			//    print("Disabled " + effectsDisabled + " finished effects.");
		}

		if (activePointPopups.Count > 0)
		{
			for (int i = 0; i < activePointPopups.Count; i++)
			{
				if (activePointPopups[i].position == pointPopupDestination.position)
				{
					//TOOD: Spawn sparkle (or other suitable) small effect here

					if (OnPointPopupEffectFinished != null)
					{
						OnPointPopupEffectFinished(int.Parse(activePointPopups[i].GetComponentInChildren<Text>().text));
					}

					activePointPopups[i].gameObject.SetActive(false);
					activePointPopups.RemoveAt(i);
					i--;

					continue;
				}

				Vector3 directionToDestination = pointPopupDestination.position - activePointPopups[i].position;
				float distanceToDestination = directionToDestination.magnitude;

				if (distanceToDestination <= pointPopupMovementSpeed * Time.deltaTime)
				{
					activePointPopups[i].position = pointPopupDestination.position;
				}
				else
				{
					activePointPopups[i].position += directionToDestination / distanceToDestination * pointPopupMovementSpeed * Time.deltaTime;
				}
			}
		}
	}

	public void SpawnSelectionEffectAtIndex(IntVector2 gridIndex)
	{
		Vector3 spawnPosZ = Vector3.zero;
		spawnPosZ.z = selectionEffectZPos;
		GameObject newEffect = poolManager.SpawnFromPool(selectionEffectPoolTag);
		newEffect.transform.SetParent(grid.GetGridElementDataFromIndex(gridIndex).elementTransform);
		newEffect.transform.localPosition = spawnPosZ;
		newEffect.SetActive(true);

		selectionEffectInfos.Add(new SelectionEffectInfo(gridIndex, newEffect));
	}

	public void ClearSelectionEffectAtIndex(IntVector2 gridIndex)
	{
		for (int i = 0; i < selectionEffectInfos.Count; i++)
		{
			if (selectionEffectInfos[i].gridIndex == gridIndex)
			{
				if (selectionEffectInfos[i].selectionEffect != null)
				{
					selectionEffectInfos[i].selectionEffect.SetActive(false);
					selectionEffectInfos[i].selectionEffect.transform.SetParent(this.transform);
				}
				selectionEffectInfos.RemoveAt(i);
				//print("Removed selection effect at index: " + gridIndex);
				break;
			}
		}
	}

	public void ClearAllSelectionEffects()
	{
		while (selectionEffectInfos.Count > 0)
		{
			if (selectionEffectInfos[0].selectionEffect != null)
			{
				selectionEffectInfos[0].selectionEffect.SetActive(false);
				selectionEffectInfos[0].selectionEffect.transform.SetParent(this.transform);
			}
			selectionEffectInfos.RemoveAt(0);
		}

		selectionEffectInfos = new List<SelectionEffectInfo>();
	}

	public void StartSelectionLine(IntVector2 startIndex)
	{
		Vector3 startPos = grid.CalculateWorldPos(startIndex);
		startPos.z = selectionLineZPos;

		Vector3[] newLinePositions = new Vector3[1] { startPos };
		selectionLine.positionCount = newLinePositions.Length;
		selectionLine.SetPositions(newLinePositions);
	}

	public void AddPointToSelectionLine(IntVector2 newPointIndex)
	{
		Vector3 newPoint = grid.CalculateWorldPos(newPointIndex);
		newPoint.z = selectionLineZPos;

		Vector3[] newLinePositions = new Vector3[selectionLine.positionCount + 1];
		for (int i = 0; i < selectionLine.positionCount; i++)
		{
			newLinePositions[i] = selectionLine.GetPosition(i);
		}

		newLinePositions[newLinePositions.Length - 1] = newPoint;
		selectionLine.positionCount = newLinePositions.Length;
		selectionLine.SetPositions(newLinePositions);
	}

	public void ClearSelectionLine()
	{
		selectionLine.positionCount = 0;
		selectionLine.material = validSelectionMaterial;
	}

	public void InvalidateSelectionLine()
	{
		selectionLine.material = invalidSelectionMaterial;
	}

	public void HighlightIndices(List<IntVector2> indicesToHighlight)
	{
		var gridWidth = grid.GridWidth;
		var gridHeight = grid.GridHeight;

		if (displayConnectedMatchesHighlight)
		{
			for (int x = 0; x < gridWidth; x++)
			{
				for (int y = 0; y < gridHeight; y++)
				{
					bool ignoreIndex = false;
					for (int i = 0; i < indicesToHighlight.Count; i++)
					{
						if (indicesToHighlight[i] == new IntVector2(x, y))
						{
							ignoreIndex = true;
						}
					}

					if (ignoreIndex)
					{
						continue;
					}

					grid.GetGridElementDataFromIndex(new IntVector2(x, y)).elementTransform.GetComponentInChildren<Renderer>().material.color = nonHighlightedElementColor;
				}
			}
		}
	}

	public void ClearHighlights()
	{
		var gridWidth = grid.GridWidth;
		var gridHeight = grid.GridHeight;

		if (displayConnectedMatchesHighlight)
		{
			for (int x = 0; x < gridWidth; x++)
			{
				for (int y = 0; y < gridHeight; y++)
				{
					if (grid.IsEmptyCell(x, y)) continue;

					grid.GetGridElementDataFromIndex(new IntVector2(x, y)).elementTransform.GetComponentInChildren<Renderer>().material.color = defaultElementColor;
				}
			}
		}
	}

	public void SpawnCollectionEffectOnIndex(IntVector2 gridIndex)
	{
		var effect = poolManager.SpawnFromPool(grid.GetGridElementDataFromIndex(gridIndex).elementType.collectionEffectPoolTag);
		var spawnPos = grid.CalculateWorldPos(gridIndex);
		spawnPos.z = collectionEffectZPos;
		effect.transform.position = spawnPos;

		collectionEffectSpawnTimes.Enqueue(Time.time);
		activeCollectionEffects.Enqueue(effect);
		effect.SetActive(true);
		effect.GetComponent<ParticleSystem>().Play(true);
	}

	public void SpawnPointPopUpsForMatch(List<IntVector2> matchElementIndices)
	{
		//print("matchElementIndices.Count: " + matchElementIndices.Count);
		int elementCount = matchElementIndices.Count;
		for (int i = 0; i < elementCount; i++)
		{
			GameObject newPointPopup = poolManager.SpawnFromPool(pointPopupPoolTag);
			newPointPopup.GetComponentInChildren<Text>().text = elementCount.ToString();
			newPointPopup.transform.position = Camera.main.WorldToScreenPoint(grid.GetGridElementDataFromIndex(matchElementIndices[i]).correctWorldPos);
			newPointPopup.SetActive(true);
			activePointPopups.Add(newPointPopup.transform);
		}
	}

	public void Restart()
	{
		//Reset and clear collection effects
		while (activeCollectionEffects.Count > 0)
		{
			activeCollectionEffects.Dequeue().SetActive(false);
		}

		collectionEffectSpawnTimes = new Queue<float>();
		activeCollectionEffects = new Queue<GameObject>();

		//Reset and clear point popup effects
		for (int i = 0; i < activePointPopups.Count; i++)
		{
			activePointPopups[i].gameObject.SetActive(false);
		}

		activePointPopups = new List<Transform>();

		ClearSelectionLine();
		ClearAllSelectionEffects();
		ClearHighlights();
	}
}

public struct SelectionEffectInfo
{
	public IntVector2 gridIndex;
	public GameObject selectionEffect;

	public SelectionEffectInfo(IntVector2 _gridIndex, GameObject _selectionEffect)
	{
		gridIndex = _gridIndex;
		selectionEffect = _selectionEffect;
	}
}
