/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using UnityEngine;
using UnityEngine.InputSystem;

using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace Arteranos.XR
{
    /// <summary>
    /// Extension of the XRGrabInteractable to make it possible to
    /// transfer the vectors from the client's throw-on-detach to the server-
    /// </summary>
    public class ArteranosGrabInteractable : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
    {
        Rigidbody m_Rigidbody;

        public Vector3 m_DetachVelocity { get; private set; }
        public Vector3 m_DetachAngularVelocity { get; private set; }

        public event Action<Vector3, Vector3> OnDetach;

        protected override void Drop()
        {
            base.Drop();
        }

        protected override void Detach()
        {
            if (m_Rigidbody == null && !TryGetComponent(out m_Rigidbody))
                throw new InvalidOperationException("No RigidBody");

            if(!m_Rigidbody.isKinematic)
            {
                base.Detach();

                m_DetachVelocity = m_Rigidbody.linearVelocity;
                m_DetachAngularVelocity = m_Rigidbody.angularVelocity;
            }
            else
            {
                // Either client or misconfigured. Temporarily turn off isKinematic
                // to convince XRGrabInteractable to spill its ~~privates~~ guts.
                m_Rigidbody.isKinematic = false;
                base.Detach();

                m_DetachVelocity = m_Rigidbody.linearVelocity;
                m_DetachAngularVelocity = m_Rigidbody.angularVelocity;

                // Swtting isKinematic to true back on zeros the velocities.
                m_Rigidbody.isKinematic = true;
            }

            // Debug.Log($"Detach velocities: {m_DetachVelocity}, {m_DetachAngularVelocity}");

            OnDetach?.Invoke(m_DetachVelocity, m_DetachAngularVelocity);
        }
    }
}
