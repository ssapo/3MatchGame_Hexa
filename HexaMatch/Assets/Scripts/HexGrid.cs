using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Random = UnityEngine.Random;
using Pattern = System.Tuple<IntVector2, int>;
using System.Linq;

public class HexGrid : MonoBehaviour
{
	public delegate void DVoidListListIntVector2(List<List<IntVector2>> listListVec2);
	public event DVoidListListIntVector2 OnAutoMatchesFound;
	public delegate void DVoidInt(int value);
	public event DVoidInt OnSuccessMoves;
	public event DVoidInt OnDestroyWood;

	public Transform hexBasePrefab;
	public TargetGoal hexWoodPrefab;
	public ElementType[] elementTypes;
	public ElementType[] spearElementTypes;

	public string gridElementTransformPoolTag;
	public string WoodTileTransformPoolTag;

	// 이거 거꾸로가 맞음 햇갈 리지말것..!!
	private int[] board = {
		0, 0, 0, 0, 1, 0, 0, 0, 0,
		0, 0, 0, 6, 5, 6, 0, 0, 0,
		1, 5, 6, 6, 6, 6, 6, 5, 1,
		0, 6, 6, 6, 6, 6, 6, 6, 0,
		0, 6, 6, 6, 6, 6, 6, 6, 0,
		0, 5, 6, 6, 6, 6, 6, 5, 0,
		1, 0, 6, 6, 6, 6, 6, 0, 1,
		0, 0, 0, 0, 6, 0, 0, 0, 0,
		0, 0, 0, 0, 6, 0, 0, 0, 0,
	};

	private static readonly int boardWidth = 9;

	public int minAutoMatchConnection = 10;
	public int minSpearMatchConnection = 4;
	public float gap = 0.1f;
	public float minNewElementSpawnYPos = 2f;
	public bool spawnHexBases = true;

	private TargetGoal[] targetGoals;

	private GridElementData[,] gridElements;
	private PoolManager PoolManager;
	private EffectManager effectManager;

	private Coroutine elementMovementCoroutine;
	private Vector3 startPos;
	private Vector3 fallDirection = Vector3.down;
	private IntVector2[] prevSwapIndices = new IntVector2[2] { IntVector2.NullVector, IntVector2.NullVector };
	private List<Tuple<IntVector2, List<IntVector2>>> spawnSpearEffectList = new List<Tuple<IntVector2, List<IntVector2>>>();

	private float hexBaseZPos = 0.25f;
	private float hexWoodZPos = 0.125f;
	private float hexWidth = 0.8f;
	private float hexHeight = 0.8f;
	private float elementWidth = 0.5f;

	private bool isElementMovementDone = true;

	// 충돌이 나지 않는 first 셋팅
	private string typeElementString = "552313455221435040215241402011404";
	private int typeEterator = 0;
	private int spearType = 0;

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

		x = -hexWidth * 0.8f * (gridWidth / 2f);
		y = -hexHeight * 0.8f * (gridHeight / 2f);

		startPos = new Vector3(x, y, 0);
	}

	private void CreateGrid()
	{
		isElementMovementDone = false;

		if (spawnHexBases)
		{
			CreateGridBase();
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

		ClearGridWood();

		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				if (IsEmptyCell(x, y))
					continue;

				if (IsTargetCell(x, y))
				{
					int life = board[y * boardWidth + x];
					targetGoals[y * boardWidth + x] = CreateHexWoodElement(new IntVector2(x, y), life, transform);
				}
			}
		}
	}

	private void ClearGridWood()
	{
		var gridWidth = GridWidth;
		var gridHeight = GridHeight;

		if (targetGoals != null)
		{
			for (int x = 0; x < gridWidth; x++)
			{
				for (int y = 0; y < gridHeight; y++)
				{
					var goal = targetGoals[y * boardWidth + x];
					if (goal != null)
						goal.gameObject.SetActive(false);
				}
			}
		}
		
		targetGoals = new TargetGoal[gridWidth * gridHeight];
	}

	private IEnumerator InitializeGridElements()
	{
		var gridWidth = GridWidth;
		var gridHeight = GridHeight;

		gridElements = new GridElementData[gridHeight, gridWidth];

		CreateGridWood();

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

	private TargetGoal CreateHexWoodElement(IntVector2 gridIndex, int life, Transform parent)
	{
		var targetType = ChooseTargetElementType();
		if (targetType == null)
			return null;

		var hex = PoolManager.SpawnFromPool(WoodTileTransformPoolTag).GetComponent<TargetGoal>();
		hex.Initialize(life);

		var spawnPos = CalculateWorldPos(gridIndex);
		spawnPos.z = hexWoodZPos;
		hex.transform.position = spawnPos;
		hex.transform.eulerAngles = new Vector3(-90f, 0, 0);
		hex.transform.parent = parent;
		hex.name = "HexWood " + gridIndex.x + "|" + gridIndex.y;
		hex.gameObject.SetActive(true);

		gridElements[gridIndex.y, gridIndex.x] = new GridElementData(targetType, hex.transform, CalculateWorldPos(gridIndex));
		return hex;
	}

	private void CreateNewSpearElement(ElementType elementType, Vector3 spawnPos, IntVector2 gridIndex, Transform parent)
	{
		var element = PoolManager.SpawnFromPool(gridElementTransformPoolTag);
		element.GetComponentInChildren<Renderer>().material = elementType.elementMaterial[spearType];
		element.transform.position = spawnPos;
		element.transform.parent = parent;
		element.name = elementType.elementName;
		element.SetActive(true);
		gridElements[gridIndex.y, gridIndex.x] = new GridElementData(elementType, element.transform, CalculateWorldPos(gridIndex), spearType + 1);
	}


	private void CreateNewGridElement(ElementType elementType, Vector3 spawnPos, IntVector2 gridIndex, Transform parent)
	{
		var element = PoolManager.SpawnFromPool(gridElementTransformPoolTag);
		element.GetComponentInChildren<Renderer>().material = elementType.elementMaterial[0];
		element.transform.position = spawnPos;
		element.transform.parent = parent;
		element.name = elementType.elementName;
		element.SetActive(true);
		gridElements[gridIndex.y, gridIndex.x] = new GridElementData(elementType, element.transform, CalculateWorldPos(gridIndex));
	}

	private ElementType ChooseRandomElementType()
	{
		return elementTypes[Random.Range(0, elementTypes.Length - 1)];
	}

	private ElementType ChooseEterateElementType()
	{
		// 이게 틀리면 게임이 진행되면 안되므로 예외처리 안함
		return elementTypes[Convert.ToInt32(typeElementString[typeEterator++] - '0')];
	}

	private ElementType ChooseSpearElementType(ElementType type)
	{
		foreach (var e in spearElementTypes)
		{
			if (IsOfMatchingElementType(type, e))
				return e;
		}

		return null;
	}

	private ElementType ChooseTargetElementType()
	{
		return elementTypes[elementTypes.Length - 1];
	}

	private List<List<IntVector2>> FindMatchesOfElementType(ElementType elementType)
	{
		var allMatchingElementIndices = new List<List<IntVector2>>();
		var indicesAlreadyChecked = new HashSet<IntVector2>();

		for (int y = 0; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				if (indicesAlreadyChecked.Contains(new IntVector2(x, y))) continue;

				if (IsOfMatchingElementType(elementType, gridElements[y, x].elementType))
				{
					var matchingNeighbours = new Dictionary<int, List<IntVector2>>();
					var matchingNeighboursToCheck = new Stack<Pattern>();
					var matchingIndices = new List<IntVector2>();

					matchingNeighbours.Add(1, matchingIndices.ToList());
					matchingNeighbours.Add(2, matchingIndices.ToList());
					matchingNeighbours.Add(3, matchingIndices.ToList());

					matchingNeighboursToCheck.Push(new Pattern(new IntVector2(x, y), 1));
					matchingNeighboursToCheck.Push(new Pattern(new IntVector2(x, y), 2));
					matchingNeighboursToCheck.Push(new Pattern(new IntVector2(x, y), 3));

					while (matchingNeighboursToCheck.Count > 0)
					{
						//print(matchingNeighboursToCheck.Count);
						var peek = matchingNeighboursToCheck.Peek();
						matchingNeighboursToCheck.Pop();

						var neighbouringIndices = GetNeighbouringIndices(peek);
						foreach (var e in neighbouringIndices)
						{
							var index = e.Item1;
							if (e.Item2 != peek.Item2) continue;
							if (indicesAlreadyChecked.Contains(e.Item1)) continue;
							if (IsOfMatchingElementType(elementType, gridElements[index.y, index.x].elementType))
							{
								var list = matchingNeighbours[e.Item2];
								if (!list.Contains(e.Item1))
								{
									list.Add(e.Item1);
									matchingNeighboursToCheck.Push(e);
								}
							}
						}
					}

					foreach (var e in matchingNeighbours.Values)
					{
						if (e.Count >= minAutoMatchConnection)
						{
							var indicies = new List<IntVector2>();
							foreach (var f in e)
							{
								indicies.Add(f);
								indicesAlreadyChecked.Add(f);
							}
							allMatchingElementIndices.Add(indicies);
						}
					}
				}
			}
		}

		return allMatchingElementIndices;
	}

	public bool IsNotTargetType(ElementType otherType)
	{
		return !IsOfMatchingElementType(ChooseTargetElementType(), otherType);
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
			if (FirstMoveMatchSuccess())
			{
				OnSuccessMoves?.Invoke(-1);
				prevSwapIndices[0] = IntVector2.NullVector;
				prevSwapIndices[1] = IntVector2.NullVector;
			}

			yield return ProduceSpawnSpearEffect();
			yield return FillGridUntilFull(false);
			yield return ElementMovementFinished();
		}
		else
		{
			SwapElementsRestore(prevSwapIndices[0], prevSwapIndices[1]);
			//MoveElementsToCorrectPositions();

			SpearTypeChange();

			yield return new WaitForSeconds(0.5f);
			yield return MoveAllElementsTowardsCorrectWorldPositions();
		}
	}

	private void SpearTypeChange()
	{
		spearType++;
		spearType %= 3;
	}

	private IEnumerator ProduceSpawnSpearEffect()
	{
		if (spawnSpearEffectList.Count > 0)
		{
			foreach(var e in spawnSpearEffectList)
			{
				//gridElements[e.y, e.x] 
			}

			spawnSpearEffectList.Clear();

			//yield return MoveAllElementsTowardsCorrectWorldPositions();
		}
		yield return new WaitForSeconds(0.3f);
	}

	private bool FirstMoveMatchSuccess()
	{
		return prevSwapIndices[0] != IntVector2.NullVector && prevSwapIndices[1] != IntVector2.NullVector;
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
					//gridElements[indicesToMove[i].y, indicesToMove[i].x].justSpawned = false;
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
		float x, y = 0;

		float yOffset = (pX % 2 == 0) ? 0 : hexHeight / 2;

		x = startPos.x + pX * hexWidth * 0.75f;
		y = startPos.y + (pY * hexHeight) + yOffset;
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

	public List<Pattern> GetNeighbouringIndices(Pattern p)
	{
		var neighbours = new List<Pattern>();

		var index = p.Item1;
		var type = p.Item2;

		switch (type)
		{
			// +x 직선
			case 1:
				{
					if (index.x % 2 == 0)
					{
						var bIndex = new IntVector2(index.x + 1, index.y - 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));

						bIndex = new IntVector2(index.x - 1, index.y);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));
					}
					else
					{
						var bIndex = new IntVector2(index.x + 1, index.y);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));

						bIndex = new IntVector2(index.x - 1, index.y + 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));
					}
				}
				break;

			// +x -y 직선
			case 2:
				{
					if (index.x % 2 == 0)
					{
						var bIndex = new IntVector2(index.x + 1, index.y);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));

						bIndex = new IntVector2(index.x - 1, index.y - 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));
					}
					else
					{
						var bIndex = new IntVector2(index.x + 1, index.y + 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));

						bIndex = new IntVector2(index.x - 1, index.y);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));
					}
				}
				break;

			// +y 직선
			case 3:
				{
					if (index.x % 2 == 0)
					{
						var bIndex = new IntVector2(index.x, index.y + 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));

						bIndex = new IntVector2(index.x, index.y - 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));
					}
					else
					{
						var bIndex = new IntVector2(index.x, index.y + 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));

						bIndex = new IntVector2(index.x, index.y - 1);
						if (CheckIfNeighbours(index, bIndex))
							neighbours.Add(new Pattern(bIndex, p.Item2));
					}
				}
				break;

		
			default:
				break;
		}

		return neighbours;
	}

	public bool CheckIfNeighbours(IntVector2 aIndex, IntVector2 bIndex)
	{
		if (aIndex.x % 2 == 0)
		{
			return (!IsEmptyCell(bIndex.x, bIndex.y) && !IsEmptyCell(aIndex.x, aIndex.y))
				&& aIndex != bIndex
				&& (aIndex.x + 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x + 1 == bIndex.x && aIndex.y - 1 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y - 1 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y - 1 == bIndex.y);
		}
		else
		{
			return (!IsEmptyCell(bIndex.x, bIndex.y) && !IsEmptyCell(aIndex.x, aIndex.y))
				&& aIndex != bIndex
				&& (aIndex.x + 1 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x + 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x - 1 == bIndex.x && aIndex.y + 0 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y + 1 == bIndex.y
				|| aIndex.x + 0 == bIndex.x && aIndex.y - 1 == bIndex.y);
		}
	}

	List<IntVector2> GetWoodsIndiciesWithNeighboursIndicies(List<IntVector2> indicies)
	{
		var goalList = new List<IntVector2>();

		foreach (var e in indicies)
		{
			IntVector2[] neighborIndicies = null;
			if (e.x % 2 == 0)
			{
				neighborIndicies = new IntVector2[]{
					new IntVector2(e.x + 1, e.y + 0),
					new IntVector2(e.x - 1, e.y + 0),
					new IntVector2(e.x + 1, e.y - 1),
					new IntVector2(e.x - 1, e.y - 1),
					new IntVector2(e.x + 0, e.y + 1),
					new IntVector2(e.x + 0, e.y - 1)
				};
			}
			else
			{
				neighborIndicies = new IntVector2[]{
					new IntVector2(e.x + 1, e.y + 1),
					new IntVector2(e.x - 1, e.y + 1),
					new IntVector2(e.x + 1, e.y + 0),
					new IntVector2(e.x - 1, e.y + 0),
					new IntVector2(e.x + 0, e.y + 1),
					new IntVector2(e.x + 0, e.y - 1)
				};
			}

			foreach (var f in neighborIndicies)
			{
				if (!CheckIfNeighbours(f, e))
					continue;

				//print(f);
				var goal = targetGoals[f.y * boardWidth + f.x];
				if (goal == null)
					continue;

				if (!goalList.Contains(f))
					goalList.Add(new IntVector2(f.x, f.y));
			}
		}

		if (goalList.Count <= 0)
			goalList = null;

		return goalList;
	}

	public int RemoveExistingMatches(bool spawnCollectionEffect = true)
	{
		var matchIndices = new List<List<IntVector2>>();
		for (int i = 0; i < elementTypes.Length; i++)
		{
			matchIndices.AddRange(FindMatchesOfElementType(elementTypes[i]));
		}

		if (matchIndices.Count > 0)
		{
			foreach (var e in matchIndices)
			{
				var goalList = GetWoodsIndiciesWithNeighboursIndicies(e);
				if (goalList != null)
				{ 
					foreach (var f in goalList)
						DamageToTarget(f);
				}

				if (e.Count >= minSpearMatchConnection)
				{
					var index = IntVector2.NullVector;
					if (FirstMoveMatchSuccess())
					{
						if (e.Contains(prevSwapIndices[0]))
							index = prevSwapIndices[0];
						else
							index = prevSwapIndices[1];
					}
					else
					{
						index = e[2];
					}

					var elementType = gridElements[index.y, index.x].elementType;
					var elementTransform = gridElements[index.y, index.x].elementTransform;

					RemoveElementAtIndex(index, spawnCollectionEffect);
					CreateNewSpearElement(ChooseSpearElementType(elementType), elementTransform.position, index, transform);

					e.Remove(index);
					spawnSpearEffectList.Add(new Tuple<IntVector2, List<IntVector2>>(index, e));
				}
			}

			OnAutoMatchesFound?.Invoke(matchIndices);
		}

		var toRemoveIndices = new HashSet<IntVector2>();
		for (int i = 0; i < matchIndices.Count; ++i)
		{
			foreach (var f in matchIndices[i])
			{
				if (toRemoveIndices.Contains(f))
					continue;

				var spearType = gridElements[f.y, f.x].spearType;
				if (spearType != 0)
				{
					var spearTargets = FindElementsOfSpear(new Pattern(f, spearType));
					//foreach (var g in spearTargets)
					//	print(g);
					matchIndices.Add(spearTargets.ToList());
				}

				//if (!toRemoveIndices.Contains(f))
				toRemoveIndices.Add(f);
			}
		}

		RemoveElementsAtIndices(toRemoveIndices.ToList(), spawnCollectionEffect);
		return matchIndices.Count;
	}

	private void DamageToTarget(IntVector2 f)
	{
		var goal = targetGoals[f.y * boardWidth + f.x];
		if (goal == null)
			return;

		goal.HP = goal.HP - 1;
		if (goal.HP <= 0)
		{
			RemoveElementAtIndex(f);
			OnDestroyWood?.Invoke(-1);
			targetGoals[f.y * boardWidth + f.x] = null;
		}
	}

	private HashSet<IntVector2> FindElementsOfSpear(Pattern withPattern)
	{
		var startIndex = withPattern.Item1;
		var spearType = withPattern.Item2;
		var spearTargets = new HashSet<IntVector2>() { startIndex };
		var gridWidth = GridWidth;
		var gridHeight = GridHeight;

		switch (spearType)
		{
			case 1:
				//{
				//	for (int x = 0; x < gridWidth; ++x)
				//	{
				//		var index = IntVector2.NullVector;
				//		if (startIndex.x % 2 == 0)
				//		{
				//			int nx = x - startIndex.x;
				//			if (nx > 0) nx += 1;
				//			else if (nx < 0) nx -= 1;
				//			index = new IntVector2(x, startIndex.y + (nx / 2));
				//		}
				//		else
				//		{
				//			int nx = x - startIndex.x;
				//			//if (nx > 0) nx += 1;
				//			//else if (nx < 0) nx -= 1;
				//			index = new IntVector2(x, startIndex.y + (nx / 2));
				//		}
				//		if (IsEmptyCell(index.x, index.y))
				//			continue;

				//		if (IsTargetCell(index.x, index.y))
				//			DamageToTarget(index);
				//		else
				//			spearTargets.Add(index);
				//	}
				//}
				//break;

			case 2:
				//{
				//	for (int x = 0; x < gridWidth; ++x)
				//	{
				//		var index = IntVector2.NullVector;
				//		if (startIndex.x % 2 == 0)
				//		{
				//			int nx = x - startIndex.x;
				//			//if (nx > 0) nx += 1;
				//			//else if (nx < 0) nx -= 1; 
				//			index = new IntVector2(x, startIndex.y - (nx / 2));
				//		}
				//		else
				//		{
				//			int nx = x - startIndex.x;
				//			if (nx > 0) nx += 1;
				//			else if (nx < 0) nx -= 1;
				//			index = new IntVector2(x, startIndex.y - (nx / 2));
				//		}
				//		if (IsEmptyCell(index.x, index.y))
				//			continue;

				//		if (IsTargetCell(index.x, index.y))
				//			DamageToTarget(index);
				//		else
				//			spearTargets.Add(index);
				//	}
				//}
				//break;

			case 3:
				{
					for (int y = 0; y < gridHeight; ++y)
					{
						var index = new IntVector2(startIndex.x, y);
						if (IsEmptyCell(index.x, index.y))
							continue;

						if (IsTargetCell(index.x, index.y))
							DamageToTarget(index);
						else
							spearTargets.Add(index);
					}
				}
				break;
		}

		return spearTargets;
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
		for (int y = 1; y < gridElements.GetLength(0); y++)
		{
			for (int x = 0; x < gridElements.GetLength(1); x++)
			{
				// 비어있으면 안되는 셀인데 비어 있다는 뜻
				if ((gridElements[y, x].elementTransform == null && !IsEmptyCell(x, y)))
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
			new IntVector2(0, 6),
			new IntVector2(8, 6),
			new IntVector2(0, 2),
			new IntVector2(8, 2),
			new IntVector2(4, 8)
		};

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
				{
					CreateNewGridElement(ChooseEterateElementType(), descendingElementWorldPos, new IntVector2(e.x, e.y), transform);
				}
				else
				{
					CreateNewGridElement(ChooseRandomElementType(), descendingElementWorldPos, new IntVector2(e.x, e.y), transform);
				}
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
						if (IsOfMatchingElementType(ChooseTargetElementType(), gridElements[y, x].elementType))
							continue;

						var vectors = new List<IntVector2>(){
							new IntVector2(x, y - 1)
						};

						if (x % 2 == 1)
						{
							vectors.Add(new IntVector2(x + 1, y));
							vectors.Add(new IntVector2(x - 1, y));
						}
						else
						{
							vectors.Add(new IntVector2(x + 1, y - 1));
							vectors.Add(new IntVector2(x - 1, y - 1));
						}

						foreach (var e in vectors)
						{
							if (e.x >= 0 && e.x < gridWidth && e.y >= 0 && e.y < gridHeight)
							{
								if (IsEmptyCell(e.x, e.y) || gridElements[e.y, e.x].elementTransform != null)
								{
									continue;
								}
								else
								{
									gridElements[e.y, e.x] = gridElements[y, x];
									gridElements[y, x].elementTransform = null;

									gridElements[e.y, e.x].correctWorldPos = CalculateWorldPos(e);
									forbidden.Add(e);

									break;
								}
							}
						}
					}
				}
			}
		}
	}

	public void Restart()
	{
		StopAllCoroutines();

		typeEterator = 0;
		isElementMovementDone = false;

		RemoveAllElements();
		StartCoroutine(InitializeGridElements());
	}

	public bool IsEmptyCell(int x, int y)
	{
		if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
			return true;

		return board[y * boardWidth + x] == 0;
	}

	public bool IsTargetCell(int x, int y)
	{
		if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
			return true;

		return board[y * boardWidth + x] == 5
			|| board[y * boardWidth + x] == 1;
	}
}

public struct GridElementData
{
	public ElementType elementType;
	public Transform elementTransform;
	public Vector3 correctWorldPos;
	public int spearType;

	public GridElementData(ElementType _elementType, Transform _elementTransform, Vector3 _correctWorldPos, int type = 0)
	{
		elementType = _elementType;
		elementTransform = _elementTransform;
		correctWorldPos = _correctWorldPos;
		spearType = type;
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

	public override String ToString()
	{
		return $"[x, {x}, y {y}]";
	}

}

