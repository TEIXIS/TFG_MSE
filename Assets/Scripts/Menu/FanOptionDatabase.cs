using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "XR/Fan Menu/Option Database")]
public class FanOptionDatabase : ScriptableObject
{
    public List<FanOption> allOptions;
}