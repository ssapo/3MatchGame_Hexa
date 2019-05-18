using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ElementType")]
public class ElementType : ScriptableObject
{
    public string elementName;
    public string collectionEffectPoolTag;
    public Material elementMaterial;
    public ElementType[] matchingElements;
}
