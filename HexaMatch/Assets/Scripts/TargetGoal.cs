using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TargetGoal : MonoBehaviour
{
	public List<GameObject> lives;

	private int hp;

	public int HP
	{
		get
		{
			return hp;
		}
		set
		{
			hp = value;
			UpdateHP(hp - 1);
		}
	}

	public void Initialize(int life)
	{
		HP = life;
	}

	private void UpdateHP(int hp)
	{
		for (int i = 0; i < lives.Count; ++i)
		{
			lives[i].SetActive(false);
		}

		for (int i = 0; i < hp; ++i)
		{
			lives[i].SetActive(true);
		}
	}

}
