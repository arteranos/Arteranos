using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Arteranos.User
{
    [AddComponentMenu("User/Teleportation Area", 10)]
    public class TeleportationArea : UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable
    {
        protected override bool GenerateTeleportRequest(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor, RaycastHit raycastHit, ref UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportRequest teleportRequest)
        {
            if(!enabled || raycastHit.collider == null)
                return false;

            teleportRequest.destinationPosition = raycastHit.point;
            teleportRequest.destinationRotation = transform.rotation;
            return true;
        }

        /// <inheritdoc />
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            if(teleportationProvider == null)
                teleportationProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();

            base.OnSelectEntered(args);
        }

        /// <inheritdoc />
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            if(teleportationProvider == null)
                teleportationProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();

            base.OnSelectExited(args);
        }

        /// <inheritdoc />
        protected override void OnActivated(ActivateEventArgs args)
        {
            if(teleportationProvider == null)
                teleportationProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();

            base.OnActivated(args);
        }

        /// <inheritdoc />
        protected override void OnDeactivated(DeactivateEventArgs args)
        {
            if(teleportationProvider == null)
                teleportationProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();

            base.OnDeactivated(args);
        }

    }
}
