/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine.EventSystems;

using Arteranos.Core;

namespace Arteranos.UI
{
    public class PrefPanel_People : UIBehaviour
    {
        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            ChoiceBook choiceBook = GetComponentInChildren<ChoiceBook>(true);

            if (!Utils.IsAbleTo(UserCapabilities.CanAdminServerUsers, null))
                RestrictRemoteServerConfig(choiceBook);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        private static void RestrictRemoteServerConfig(ChoiceBook choiceBook)
        {
            int found = -1;
            for (int i = 0; i < choiceBook.ChoiceEntries.Length; i++)
                if (choiceBook.ChoiceEntries[i].name == "In Server") found = i;

            choiceBook.SetPageActive(found, false);
        }

    }
}
