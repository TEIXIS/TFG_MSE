//Fitxer per configurar les opcions que vull apareixin al menu
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "XR/Fan Menu/Menu Selection")]
public class FanMenuSelection : ScriptableObject
{
    public List<GameObject> selectedOptions;
}
