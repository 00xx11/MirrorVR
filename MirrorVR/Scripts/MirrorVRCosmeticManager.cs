using Mirror.VR;
using UnityEngine;

public class MirrorVRCosmeticManager : MonoBehaviour
{
    public enum CosmeticType
    {
        Head,
        Face,
        Body,
        LeftHand,
        RightHand,
    }

    public void EquipCosmetic(string CosmeticId, CosmeticType type)
    {
        MirrorVRPlayer.LocalInstance.EquipCosmetic(CosmeticId, type);
    }


}
