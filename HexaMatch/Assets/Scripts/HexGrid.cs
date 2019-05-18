using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGrid : MonoBehaviour
{

	public delegate void DVoidListListIntVector2(List<List<IntVector2>> listListVec2);
	public event DVoidListListIntVector2 OnAutoMatchesFound;
	public delegate void DVoidInt(int move);
	public event DVoidInt OnSuccessMoves;

	public Transform hexBasePrefab;
	public Transform hexWoodPrefab;
	public ElementType[] elementTypes;

	public string gridElementTransformPoolTag;

	// 이거 거꾸로가 맞음 착각하지마셈!!
	private int[] board = {
		1, 1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 0, 1, 1, 1, 1,
		1, 1, 1, 0, 0, 0, 1, 1, 1,
		1, 1, 0, 0, 0, 0, 0, 1, 1,
		1, 0, 0, 0, 0, 0, 0, 0, 1,
	};

	private static readonly int boardWidth = 9;

	public int minViableConnection = 2;
	public int minAutoMatchConnection = 10;

	public float gap = 0.1f;
	public float minNewElementSpawnYPos = 2f;

	public bool spawnHexBases = true;
	public bool offsetIndividualSpawns = true;

	private GridElementData[,] gridElements;
	private PoolManager PoolManager;
	private EffectManager effectManager;

	private Coroutine elementMovementCoroutine;
	private Vector3 startPos;
	private Vector3 fallDirection = Vector3.down;
	private IntVector2[] prevSwapIndices = new IntVector2[2] { IntVector2.NullVector, IntVector2.NullVector };

	private float hexBaseZPos = 0.25f;
	private float hexWoodZPos = 0.125f;
	private float hexWidth = 0.75f;
	private float hexHeight = 0.866f;
	private float elementWidth = 0.5f;

	private bool isElementMovementDone = true;

	private int typeEterator;

	public int GridWidth
	{
		get { return boardWidth; }
	}

	public int GridHeight
	{
		get { return board.Length / boardWidth; }
	}

	private void Start()
	{
		PoolManager = GetComponent<PoolManager>();
		effectManager = GetComponent<EffectManager>();

		fallDirection.Normalize();
		AddGap();
		CalculateStartPos();
		CreateGrid();
	}

	private void AddGap()
	{
		hexWidth += hexWidth * gap;
		hexHeight += hexHeight * gap;
	}

	private void CalculateStartPos()
	{
		float x, y = 0;

		var gridWidth = GridWidth;
		var gridHeight = GridHeight;

		x = -hexWidth * 0.75f * (gridWidth / 2f);
		y = -hexHeight * (gridHeight / 2f);

		startPos = new Vector3(x, y, 0);
	}

	private void CreateGrid()
	{
		isElementMovementDone = false;

		if (spawnHexBases)
		{
			CreateGridBase();
			CreateGridWood();
		}

		StartCoroutine(InitializeGridElements());
	}

	private void CreateGridBase()
	{
		var gridWidth = GridWidth;
		var gridHeight = GridHeight;

		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				if (IsEmptyCell(x, y))
					continue;

				SpawnHexBaseTile(new IntVector2(x, y));
			}
		}
	}

	private void CreateGridWood()
	{
		var gridWidth = GridWidth;
		var gridHeight = GridHeight;

		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				if (IsEmptyCell(x, y))
					continue;

				SpawnHexWoodTile(new IntVector2(x, y));
			}
		}
	}

	private IEnumerator InitializeGridElements()
	{
		var gridWidth = GridWidth;
		var gridHeight = GridHeight;

		gridElements = new GridElementData[gridHeight, gridWidth];

		yield return FillGridUntilFull(true);
		yield return MoveElementSequenceCoroutine();
	}

	private void SpawnHexBaseTile(IntVector2 gridIndex)
	{
		var hex = Instantiate(hexBasePrefab) as Transform;
		var spawnPos = CalculateWorldPos(gridIndex);
		spawnPos.z = hexBaseZPos;
		hex.position = spawnPos;
		hex.eulerAngles = new Vector3(-90f, 0, 0);

		hex.parent = this.transform;
		hex.name = "HexTile " + gridIndex.x + "|" + gridIndex.y;
	}

	private void SpawnHexWoodTile(IntVector2 gridIndex)
	{
		var hex = Instantiate(hexWoodPrefab) as Transform;
		var spawnPos = CalculateWorldPos(gridIndex);
		spawnPos.z = hexWoodZPos;
		hex.position = spawnPos;
		hex.eulerAngles = new Vector3(-90f, 0, 0);

		hex.parent = this.transform;
		hex.name = "HexWood " + gridIndex.x + "|" + gridIndex.y;
	}

	private void CreateNewGridElement(ElementType elementType, Vector3 spawnPos, IntVector2 gridIndex, Transform parent)
	{
		var element = PoolManager.SpawnFromPool(gridElementTransformPoolTag);
		element.GetComponentInChildren<Renderer>().material = elementType.elementMaterial;
		element.transform.position = spawnPos;
		element.transform.parent = parent;
		element.SetActive(true);
		gridElements[gridIndex.y, gridIndex.x] = new GridElementData(elementType, element.transform, CalculateWorldPos(gridIndex));
	}

	private ElementType ChooseRandomElementType()
	{
		return elementTypes[Random.Range(0, elementTypes.Length)];
	}

	private ElementType ChooseEterateElementType()
	{
		typeEterator++;
		typeEterator %= elementTypes.Length;
		return elementTypes[typeEterator];
	}

	private List<List<IntVector2>> FindMatchesOfElementType(ElementType elementType)
	{
		var allMatchingElementIndices = new List<List<IntVector2>>();
		var indicesAlreadyChecked = new List<IntVector2>();

		//Loop through grid elements
		for (int y = 0; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				if (gridElements[y, x].flaggedForRemovalByAutoMatch) continue;
				if (indicesAlreadyChecked.Contains(new IntVector2(x, y))) continue;

				//If the element is of matching type with the element to check against
				if (IsOfMatchingElementType(elementType, gridElements[y, x].elementType))
				{
					//print("Found element matching with the type to check at " + x + "|" + y);
					//Store the matching neighbours on a one list
					var matchingNeighbours = new List<IntVector2>();
					//And the matching neighbours whose neighbours we have yet to check, on another list
					var matchingNeighboursToCheck = new List<IntVector2>();
					//Add the current element as the first item on both lists
					matchingNeighbours.Add(new IntVector2(x, y));
					matchingNeighboursToCheck.Add(new IntVector2(x, y));

					//Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
					while (matchingNeighboursToCheck.Count > 0)
					{
						if (!indicesAlreadyChecked.Contains(matchingNeighboursToCheck[0]))
							indicesAlreadyChecked.Add(matchingNeighboursToCheck[0]);

						var neighbouringIndices = GetNeighbouringIndices(matchingNeighboursToCheck[0]);

						for (int i = 0; i < neighbouringIndices.Count; i++)
						{
							if (gridElements[neighbouringIndices[i].y, neighbouringIndices[i].x].flaggedForRemovalByAutoMatch) continue;

							if (IsOfMatchingElementType(elementType, gridElements[neighbouringIndices[i].y, neighbouringIndices[i].x].elementType))
							{
								if (!matchingNeighbours.Contains(neighbouringIndices[i]))
								{
									matchingNeighbours.Add(neighbouringIndices[i]);
									matchingNeighboursToCheck.Add(neighbouringIndices[i]);
									gridElements[neighbouringIndices[i].y, neighbouringIndices[i].x].flaggedForRemovalByAutoMatch = true;
								}
							}
						}

						//print("Removing matching neighbour at index " + matchingNeighboursToCheck[0] + " to check from list.");
						matchingNeighboursToCheck.RemoveAt(0);
					}

					if (matchingNeighbours.Count >= minAutoMatchConnection)
					{
						//print("Connected matches of index " + x + "|" + y + " checked, adding " + matchingNeighbours.Count + " indices to the match list.");
						allMatchingElementIndices.Add(matchingNeighbours);
					}
					//else
					//{
					//    print("Connected matches of index " + x + "|" + y + " checked, connected element count not big enough for match, ignoring indices (matchingNeighbours.Count: " + matchingNeighbours.Count + ").");
					//}
				}
			}
		}

		return allMatchingElementIndices;
	}

	private bool IsOfMatchingElementType(ElementType mainType, ElementType otherType)
	{
		if (otherType == mainType)
		{
			return true;
		}

		for (int i = 0; i < mainType.matchingElements.Length; i++)
		{
			if (otherType == mainType.matchingElements[i])
			{
				return true;
			}
		}

		return false;
	}

	private IEnumerator ElementMovementFinished()
	{
		if (RemoveExistingMatches() > 0)
		{
			//prevSwap이 진행되지 않아 NullVector가 아닌 상태일때 성공했다고 볼수 있음
			if (prevSwapIndices[0] != IntVector2.NullVector && prevSwapIndices[1] != IntVector2.NullVector)
			{
				OnSuccessMoves?.Invoke(-1);
				prevSwapIndices[0] = IntVector2.NullVector;
				prevSwapIndices[1] = IntVector2.NullVector;
			}

			yield return FillGridUntilFull(false);
			yield return ElementMovementFinished();
		}
		else
		{
			SwapElementsRestore(prevSwapIndices[0], prevSwapIndices[1]);
			//MoveElementsToCorrectPositions();

			yield return new WaitForSeconds(0.5f);
			yield return MoveAllElementsTowardsCorrectWorldPositions();
		}
	}

	private IEnumerator MoveAllElementsTowardsCorrectWorldPositions(float movementSpeedIncrementMultiplier = 4f)
	{
		float movementSpeed = 0f;
		float movementSpeedIncrementPerSecond = 25f;

		var indicesToMove = new List<IntVector2>();

		for (int y = 0; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				if (IsEmptyCell(x, y))
					continue;

				if (gridElements[y, x].elementTransform == null)
					continue;

				if (gridElements[y, x].elementTransform.position != gridElements[y, x].correctWorldPos)
				{
					indicesToMove.Add(new IntVector2(x, y));
				}
			}
		}

		while (indicesToMove.Count > 0)
		{
			for (int i = 0; i < indicesToMove.Count; i++)
			{
				var elementToMove = gridElements[indicesToMove[i].y, indicesToMove[i].x];

				var directionToCorrectPos = elementToMove.correctWorldPos - elementToMove.elementTransform.position;
				float distanceToCorrectPos = directionToCorrectPos.magnitude;

				if (distanceToCorrectPos <= movementSpeed * Time.deltaTime)
				{
					elementToMove.elementTransform.position = elementToMove.correctWorldPos;
					gridElements[indicesToMove[i].y, indicesToMove[i].x].justSpawned = false;
					//print("Element at " + indicesToMove[i] + " has arrived at correct world pos. indicesToMove.Count: "
					//    + indicesToMove.Count + ", i: " + i);
					indicesToMove.RemoveAt(i);
					i--;
				}
				else
				{
					directionToCorrectPos /= distanceToCorrectPos;
					elementToMove.elementTransform.position += directionToCorrectPos * movementSpeed * Time.deltaTime;
				}
			}

			movementSpeed += movementSpeedIncrementPerSecond * movementSpeedIncrementMultiplier * Time.deltaTime;

			yield return new WaitForEndOfFrame();
		}
	}

	public void MoveElementsToCorrectPositions(float movementSpeedIncrementMultiplier = 1f)
	{
		if (elementMovementCoroutine != null)
		{
			StopCoroutine(elementMovementCoroutine);
		}

		elementMovementCoroutine = StartCoroutine(MoveElementSequenceCoroutine(movementSpeedIncrementMultiplier));
	}

	private IEnumerator MoveElementSequenceCoroutine(float movementSpeedIncrementMultiplier = 1f)
	{
		isElementMovementDone = false;

		yield return MoveAllElementsTowardsCorrectWorldPositions(movementSpeedIncrementMultiplier);
		yield return ElementMovementFinished();

		isElementMovementDone = true;
	}

	public bool GetIsElementMovementDone()
	{
		return isElementMovementDone;
	}

	public List<IntVector2> FindMatchesForIndex(IntVector2 gridIndex)
	{
		var elementType = gridElements[gridIndex.y, gridIndex.x].elementType;

		//Store the matching neighbours on a one list
		var matchingNeighbours = new List<IntVector2>();
		//And the matching neighbours whose neighbours we have yet to check, on another list
		var matchingNeighboursToCheck = new List<IntVector2>();
		//Add the current element as the first item on both lists
		matchingNeighbours.Add(gridIndex);
		matchingNeighboursToCheck.Add(gridIndex);

		//Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
		while (matchingNeighboursToCheck.Count > 0)
		{
			var neighbouringIndices = GetNeighbouringIndices(matchingNeighboursToCheck[0]);

			for (int i = 0; i < neighbouringIndices.Count; i++)
			{
				if (IsOfMatchingElementType(elementType, gridElements[neighbouringIndices[i].y, neighbouringIndices[i].x].elementType))
				{
					if (!matchingNeighbours.Contains(neighbouringIndices[i]))
					{
						matchingNeighbours.Add(neighbouringIndices[i]);
						matchingNeighboursToCheck.Add(neighbouringIndices[i]);
					}
				}
			}

			matchingNeighboursToCheck.RemoveAt(0);
		}

		return matchingNeighbours;
	}

	public void SwapElementsRecord(IntVector2 aIndex, IntVector2 bIndex)
	{
		SwapElements(aIndex, bIndex);
		prevSwapIndices[0] = aIndex;
		prevSwapIndices[1] = bIndex;
	}

	public void SwapElementsRestore(IntVector2 aIndex, IntVector2 bIndex)
	{
		SwapElements(aIndex, bIndex);
		prevSwapIndices[0] = IntVector2.NullVector;
		prevSwapIndices[1] = IntVector2.NullVector;
	}

	public void SwapElements(IntVector2 aIndex, IntVector2 bIndex)
	{
		if (aIndex == IntVector2.NullVector || bIndex == IntVector2.NullVector)
			return;

		var oldA = gridElements[aIndex.y, aIndex.x];
		gridElements[aIndex.y, aIndex.x] = gridElements[bIndex.y, bIndex.x];
		gridElements[bIndex.y, bIndex.x] = oldA;

		gridElements[aIndex.y, aIndex.x].correctWorldPos = CalculateWorldPos(aIndex);
		gridElements[bIndex.y, bIndex.x].correctWorldPos = CalculateWorldPos(bIndex);
	}

	public void ResetElementWorldPos(IntVector2 gridIndex)
	{
		GridElementData element = gridElements[gridIndex.y, gridIndex.x];
		if (element.elementTransform != null)
		{
			element.correctWorldPos = CalculateWorldPos(gridIndex);
			element.elementTransform.transform.position = element.correctWorldPos;
			element.elementTransform.transform.parent = this.transform;
		}
	}

	public Vector3 CalculateWorldPos(int pX, int pY)
	{
		int[] positions = {
			0, 0, 1, 1, 2, 1, 1, 0, 0
		};

		float x, y = 0;

		float yOffset = (pX % 2 == 0) ? 0 : hexHeight / 2;

		x = startPos.x + pX * hexWidth * 0.75f;
		y = startPos.y + ((pY + positions[pX]) * hexHeight) + yOffset;
		return new Vector3(x, y, 0);
	}

	public Vector3 CalculateWorldPos(IntVector2 gridPos)
	{
		return CalculateWorldPos(gridPos.x, gridPos.y);
	}

	public IntVector2 GetGridIndexFromWorldPosition(Vector3 worldPos, bool limitToElementWidth = false)
	{
		for (int y = 0; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				var gridWorldPos = CalculateWorldPos(new IntVector2(x, y));
				float halfAreaSize = limitToElementWidth ? elementWidth / 2 : (hexHeight - gap) / 2;
				float xMin = gridWorldPos.x - halfAreaSize;
				float xMax = gridWorldPos.x + halfAreaSize;
				float yMin = gridWorldPos.y - halfAreaSize;
				float yMax = gridWorldPos.y + halfAreaSize;

				if (worldPos.x >= xMin && worldPos.x <= xMax && worldPos.y >= yMin && worldPos.y <= yMax)
				{
					var matchingGridIndices = new IntVector2(x, y);
					return matchingGridIndices;
				}
			}
		}

		return IntVector2.NullVector;
	}

	public GridElementData GetGridElementDataFromIndex(IntVector2 gridIndex)
	{
		return gridElements[gridIndex.y, gridIndex.x];
	}

	public List<IntVector2> GetNeighbouringIndices(IntVector2 gridIndex)
	{
		var neighbours = new List<IntVector2>();

		for (int y = gridIndex.y - 1; y <= gridIndex.y + 1; y++)
		{
			if (y < 0 || y >= gridElements.GetLength(0))
				continue;


			for (int x = gridIndex.x - 1; x <= gridIndex.x + 1; x++)
			{
				if (x < 0 || x >= gridElements.GetLength(1))
					continue;

				if (CheckIfNeighbours(gridIndex, new IntVector2(x, y)))
					neighbours.Add(new IntVector2(x, y));
			}
		}

		//print("Element " + gridIndex.x + "|" + gridIndex.y + " neighbour count: " + neighbours.Count);

		return neighbours;
	}

	public bool CheckIfNeighbours(IntVector2 aIndex, IntVector2 bIndex)
	{
		int centerX = (GridWidth / 2);
		if (aIndex.x > centerX)
		{
			return aIndex.x + 1 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x + 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y - 1 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y - 1 == bIndex.y;
		}
		else if (aIndex.x == centerX)
		{
			return aIndex.x + 1 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x + 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y + 0 == bIndex.y;
		}
		else
		{
			return aIndex.x - 1 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x + 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x + 1 == bIndex.x && aIndex.y - 1 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y - 1 == bIndex.y;
		}
	}

	public int RemoveExistingMatches(bool ignoreCallbackEvent = false, bool spawnCollectionEffect = true)
	{
		var matchIndices = new List<List<IntVector2>>();
		for (int i = 0; i < elementTypes.Length; i++)
		{
			matchIndices.AddRange(FindMatchesOfElementType(elementTypes[i]));
		}

		//Reset flaggedForRemovalByAutoMatch flags
		for (int y = 0; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				gridElements[y, x].flaggedForRemovalByAutoMatch = false;
			}
		}

		if (!ignoreCallbackEvent && OnAutoMatchesFound != null)
			OnAutoMatchesFound(matchIndices);

		int removedElementsCount = 0;
		for (int j = 0; j < matchIndices.Count; j++)
		{
			RemoveElementsAtIndices(matchIndices[j], spawnCollectionEffect);
			removedElementsCount += matchIndices[j].Count;
		}
		//print("Removed " + removedElementsCount + " elements due to auto-matching.");

		return matchIndices.Count;
	}

	public void RemoveElementAtIndex(IntVector2 gridIndex, bool disableElementTransform = true, bool spawnCollectionElement = true)
	{
		if (spawnCollectionElement)
		{
			effectManager.SpawnCollectionEffectOnIndex(gridIndex);
		}

		if (disableElementTransform)
		{
			gridElements[gridIndex.y, gridIndex.x].elementTransform.gameObject.SetActive(false);
		}

		gridElements[gridIndex.y, gridIndex.x].elementTransform = null;
		gridElements[gridIndex.y, gridIndex.x].elementType = null;
	}

	public void RemoveElementsAtIndices(List<IntVector2> gridIndices, bool spawnCollectionEffects = true)
	{
		for (int i = 0; i < gridIndices.Count; i++)
		{
			RemoveElementAtIndex(gridIndices[i], spawnCollectionElement: spawnCollectionEffects);
		}
	}

	public void RemoveAllElements()
	{
		for (int y = 0; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				if (IsEmptyCell(x, y)) continue;
				if (gridElements[y, x].elementTransform == null) continue;

				gridElements[y, x].elementTransform.gameObject.SetActive(false);
			}
		}

		gridElements = new GridElementData[GridHeight, GridWidth];
	}

	private bool CheckForEmptyGridIndices()
	{
		for (int y = 0; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				// 비어있으면 안되는 셀인데 비어 있다는 뜻
				if (gridElements[y, x].elementTransform == null && !IsEmptyCell(x, y))
				{
					return true;
				}
			}
		}

		return false;
	}


	public IEnumerator FillGridUntilFull(bool first)
	{
		var factoryIndcies = new List<IntVector2>() {
			new IntVector2(0, 4),
			new IntVector2(8, 4)
		};

		int centerX = GridWidth / 2;
		int gridWidth = GridWidth;
		int gridHeight = GridHeight;

		while (CheckForEmptyGridIndices())
		{
			foreach (var e in factoryIndcies)
			{
				if (IsEmptyCell(e.x, e.y))
					continue;

				if (gridElements[e.y, e.x].elementTransform != null)
					continue;

				var correctWorldPos = CalculateWorldPos(new IntVector2(e.x, e.y));
				var descendingElementWorldPos = correctWorldPos - fallDirection * hexHeight;

				if (first)
					CreateNewGridElement(ChooseEterateElementType(), descendingElementWorldPos, new IntVector2(e.x, e.y), transform);
				else
					CreateNewGridElement(ChooseRandomElementType(), descendingElementWorldPos, new IntVector2(e.x, e.y), transform);
			}

			if (first)
				yield return MoveAllElementsTowardsCorrectWorldPositions(8f);
			else
				yield return MoveAllElementsTowardsCorrectWorldPositions();

			var forbidden = new HashSet<IntVector2>();
			//Find all empty indices
			for (int y = 0; y < gridElements.GetLength(0); y++)
			{
				for (int x = 0; x < gridElements.GetLength(1); x++)
				{
					if (IsEmptyCell(x, y))
						continue;

					if (forbidden.Contains(new IntVector2(x, y)))
						continue;

					if (gridElements[y, x].elementTransform != null)
					{
						var nV = new IntVector2(x, y - 1);
						if (nV.x >= 0 && nV.x < gridWidth && nV.y >= 0 && nV.y < gridHeight)
						{
							if (IsEmptyCell(nV.x, nV.y) || gridElements[nV.y, nV.x].elementTransform != null)
							{
								nV.x = x + ((centerX > x) ? 1 : -1);

								if (nV.x >= 0 && nV.x < gridWidth && nV.y >= 0 && nV.y < gridHeight)
								{
									if (IsEmptyCell(nV.x, nV.y) || gridElements[nV.y, nV.x].elementTransform != null)
									{
										continue;
									}
									else
									{
										gridElements[nV.y, nV.x] = gridElements[y, x];
										gridElements[y, x].elementTransform = null;

										gridElements[nV.y, nV.x].correctWorldPos = CalculateWorldPos(nV);
										forbidden.Add(nV);
									}
								}
							}
							else
							{
								gridElements[nV.y, nV.x] = gridElements[y, x];
								gridElements[y, x].elementTransform = null;

								gridElements[nV.y, nV.x].correctWorldPos = CalculateWorldPos(nV);
								forbidden.Add(nV);
							}
						}
					}
				}
			}
		}
	}

	public void Restart()
	{
		isElementMovementDone = false;

		RemoveAllElements();
		StartCoroutine(InitializeGridElements());
	}

	public bool IsEmptyCell(int x, int y)
	{
		return board[y * boardWidth + x] == 0;
	}
}

public struct GridElementData
{
	public ElementType elementType;
	public Transform elementTransform;
	public Vector3 correctWorldPos;
	public bool justSpawned;
	public bool flaggedForRemovalByAutoMatch;

	public GridElementData(ElementType _elementType, Transform _elementTransform, Vector3 _correctWorldPos)
	{
		elementType = _elementType;
		elementTransform = _elementTransform;
		correctWorldPos = _correctWorldPos;
		justSpawned = true;
		flaggedForRemovalByAutoMatch = false;
	}
}

public class IntVector2
{
	public readonly static IntVector2 NullVector = new IntVector2(-1, -1);

	public int x, y;

	public IntVector2(int _x, int _y)
	{
		x = _x;
		y = _y;
	}

	public override int GetHashCode()
	{
		return x ^ y;
	}

	public override bool Equals(object obj)
	{
		if (obj == null) return false;

		var iv2 = obj as IntVector2;
		if ((object)iv2 == null) return false;

		return Equals(iv2);
	}

	public bool Equals(IntVector2 other)
	{
		return (x == other.x && y == other.y);
	}

	public static bool operator ==(IntVector2 a, IntVector2 b)
	{
		return (a.x == b.x && a.y == b.y);
	}

	public static bool operator !=(IntVector2 a, IntVector2 b)
	{
		return (a.x != b.x || a.y != b.y);
	}

}

