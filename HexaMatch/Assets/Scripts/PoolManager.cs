using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
	[System.Serializable]
	public struct Pool
	{
		public string tag;
		public GameObject prefab;
		public int minSize;
	}

	public Transform canvasTransform;
	public List<Pool> pools;
	public List<Pool> uiObjectPools;

	private Dictionary<string, List<GameObject>> poolDictionary;
	private Dictionary<string, List<GameObject>> uiObjectPoolDictionary;
	private Transform poolHolder;

	private void Awake()
	{
		poolHolder = new GameObject("PoolHolder").transform;
		poolHolder.SetParent(this.transform);

		poolDictionary = new Dictionary<string, List<GameObject>>();
		uiObjectPoolDictionary = new Dictionary<string, List<GameObject>>();

		foreach (Pool pool in pools)
		{
			poolDictionary.Add(pool.tag, InitializePoolObjects(pool, false));
		}

		foreach (Pool uiObjectPool in uiObjectPools)
		{
			uiObjectPoolDictionary.Add(uiObjectPool.tag, InitializePoolObjects(uiObjectPool, true));
		}
	}

	private List<GameObject> InitializePoolObjects(Pool pool, bool uiObjectPool)
	{
		List<GameObject> objectPool = new List<GameObject>();

		for (int i = 0; i < pool.minSize; i++)
		{
			Transform initialParent = uiObjectPool ? canvasTransform : poolHolder;
			GameObject newOBject = Instantiate(pool.prefab, initialParent);
			newOBject.SetActive(false);
			objectPool.Add(newOBject);
		}

		//print("Initialized pool for objects with tag " + pool.tag);
		return objectPool;
	}

	private GameObject AddObjectToPool(string tag, bool uiObjectPool)
	{
		Transform initialParent = uiObjectPool ? canvasTransform : poolHolder;
		List<Pool> poolList = uiObjectPool ? uiObjectPools : pools;
		GameObject objectToSpawn = null;

		for (int j = 0; j < poolList.Count; j++)
		{
			if (poolList[j].tag == tag)
			{
				objectToSpawn = Instantiate(poolList[j].prefab, initialParent);
				objectToSpawn.SetActive(false);
				break;
			}
		}

		if (objectToSpawn != null)
		{
			if (uiObjectPool)
				uiObjectPoolDictionary[tag].Add(objectToSpawn);
			else
				poolDictionary[tag].Add(objectToSpawn);
		}
		else
		{
			Debug.LogWarning("No pool with tag " + tag + " was found when trying to create and add a new object to pool.");
		}

		return objectToSpawn;
	}

	private GameObject FindAvailableObjectFromPool(string tag, bool uiObjectPool)
	{
		List<GameObject> objectPool = uiObjectPool ? uiObjectPoolDictionary[tag] : poolDictionary[tag];

		for (int i = 0; i < objectPool.Count; i++)
		{
			if (objectPool[i].activeSelf == false)
			{
				return objectPool[i];
			}
		}

		return null;
	}

	public GameObject SpawnFromPool(string tag)
	{
		if (poolDictionary.ContainsKey(tag))
		{
			//print("Non-UI object pool found with tag " + tag + ".");
			//Find available object from pool
			GameObject objectFromPool = FindAvailableObjectFromPool(tag, false);
			if (objectFromPool != null)
			{
				return objectFromPool;
			}

			//If no objects available in pool, create and add a new one to the pool
			objectFromPool = AddObjectToPool(tag, false);
			if (objectFromPool != null)
			{
				return objectFromPool;
			}
		}
		else if (uiObjectPoolDictionary.ContainsKey(tag))
		{

			//print("UI object pool found with tag " + tag + ".");
			//Find available object from pool
			GameObject objectFromPool = FindAvailableObjectFromPool(tag, true);
			if (objectFromPool != null)
			{
				return objectFromPool;
			}

			//If no objects available in pool, create and add a new one to the pool
			objectFromPool = AddObjectToPool(tag, true);
			if (objectFromPool != null)
			{
				return objectFromPool;
			}
		}
		else
		{
			Debug.LogWarning("Pool with tag " + tag + " does not exist.");
		}

		return null;
	}

}
