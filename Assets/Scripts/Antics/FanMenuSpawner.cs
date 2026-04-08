using UnityEngine;
using UnityEngine.InputSystem;
public class FanMenuSpawner : MonoBehaviour
{
    public FanMenu menu;
    public Transform head;
    [Header("Hand Transforms")]
    public Transform leftHand;
    public Transform rightHand;

    public float distancia;
    public void OpenMenu()
    {
        menu.PlaceInFrontOfUser(head, leftHand, rightHand, distancia, 0.05f);
        menu.Build();
    }
    

}