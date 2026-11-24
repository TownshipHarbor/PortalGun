using System;
using UnityEngine;
using Il2Cpp;

namespace PortalGunQuest
{
    internal static class Grip
    {
        internal static int Fixed;

        internal static bool Before(Pickup __instance, Interactor interactor)
        {
            try
            {
                if (__instance == null || interactor == null || !Live.IsGun(__instance)) return true;
                var points = __instance.GrabPoints;
                if (points == null || points.Length == 0) return true;
                GrabPoint point = points[0];
                Transform origin = __instance.transform;
                bool left = interactor.Controller != null && interactor.Controller.Hand != null && interactor.Controller.Hand.HandIndex == HandIndex.Left;
                point.CheckScore(origin, 1f, 1f, 0.005f, origin.InverseTransformPoint(interactor.InteractionPoint.position), Quaternion.Inverse(origin.rotation) * interactor.InteractionPoint.rotation, left, false);
                var hand = interactor.Controller != null ? interactor.Controller.Hand : null;
                Transform wrist = hand != null && hand.MasterHand != null ? hand.MasterHand.IKHandRoot : null;
                Quaternion aim = interactor.Controller != null ? interactor.Controller.transform.rotation : interactor.InteractionPoint.rotation;
                if (wrist != null && wrist.childCount >= 5)
                {
                    Vector3 forward = wrist.GetChild(2).position - wrist.position;
                    Vector3 up = wrist.GetChild(0).position - wrist.GetChild(1).position;
                    if (forward.sqrMagnitude > 0.00001f && Vector3.Cross(forward, up).sqrMagnitude > 0.00000001f)
                        aim = TransitMath.FistRotation(wrist.position, wrist.GetChild(2).position, wrist.GetChild(0).position, wrist.GetChild(1).position);
                }
                point.LastRotation = Quaternion.Inverse(aim) * interactor.InteractionPoint.rotation;
                __instance.SetupGrabPoint(interactor, point);
                if (Fixed++ == 0) Core.Log("gun grip set from the hand");
                return false;
            }
            catch (Exception e)
            {
                Core.Error("grip: " + e.Message);
                return true;
            }
        }
    }
}
