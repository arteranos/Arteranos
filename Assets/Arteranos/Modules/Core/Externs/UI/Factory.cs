/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Avatar;
using Arteranos.Core;
using Arteranos.Common.Cryptography;
using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arteranos.UI
{
    public static class Factory
    {
        public static IAgreementDialogUI NewAgreement(ServerInfo serverInfo, Action disagree, Action agree)
        {
            static bool CanSkipAgreement(ServerInfo si)
            {
                Client client = G.Client;
                byte[] serverTOSHash = si.PrivacyTOSNoticeHash;
                if (serverTOSHash == null) return true;

                if (client.ServerPasses.TryGetValue(si.SPKDBKey, out ServerPass sp))
                {
                    // Server's [custom] agreement is unchanged.
                    if (sp.PrivacyTOSHash != null && serverTOSHash.SequenceEqual(sp.PrivacyTOSHash)) return true;
                }

                // Server uses an unknown TOS deviating from the standard TOS, needs to ask.
                if (si.UsesCustomTOS) return false;

                byte[] currentTOSHash = Hashes.SHA256(SettingsManager.DefaultTOStext);

                // Only if the user's knowledge of the default TOS is up to date.
                return client.KnowsDefaultTOS != null && currentTOSHash.SequenceEqual(client.KnowsDefaultTOS);

            }

            string text = serverInfo?.PrivacyTOSNotice;

            // Can we skip it? Pleeeease..?
            if (text == null || CanSkipAgreement(serverInfo))
            {
                Client.UpdateServerPass(serverInfo, true, null);
                agree?.Invoke();
                return null;
            }

            GameObject go = Object.Instantiate(BP.I.UI.AgreementDialog);
            IAgreementDialogUI AgreementDialogUI = go.GetComponent<IAgreementDialogUI>();
            AgreementDialogUI.OnDisagree += disagree;
            AgreementDialogUI.OnAgree += agree;
            AgreementDialogUI.ServerInfo = serverInfo;
            return AgreementDialogUI;
        }

        public static IDialogUI NewDialog()
        {
            GameObject go = Object.Instantiate(BP.I.UI.Dialog);
            return go.GetComponent<IDialogUI>();
        }

        public static IProgressUI NewProgress()
        {
            GameObject go = Object.Instantiate(BP.I.UI.Progress);
            return go.GetComponent<IProgressUI>();
        }

        public static INameplateUI NewNameplate(GameObject bearer)
        {
            GameObject go;
            INameplateUI nameplate;

            nameplate = bearer.GetComponentInChildren<INameplateUI>(true);
            go = nameplate?.gameObject;

            if(nameplate == null)
            {
                GameObject original = BP.I.InApp.Nameplate;
                original.SetActive(false);

                go = Object.Instantiate(original, bearer.transform);

                nameplate = go.GetComponent<INameplateUI>();
            }

            nameplate.Bearer = bearer.GetComponent<IAvatarBrain>();
            go.SetActive(true);

            return nameplate;
        }

        public static ITextMessageUI NewTextMessage(IAvatarBrain receiver)
        {
            GameObject go = Object.Instantiate(BP.I.UI.TextMessage);
            ITextMessageUI textMessageUI = go.GetComponent<ITextMessageUI>();
            textMessageUI.Receiver = receiver;
            return textMessageUI;
        }

        public static IKickBanUI NewKickBan(IAvatarBrain target)
        {
            GameObject go = Object.Instantiate(BP.I.UI.KickBan);
            IKickBanUI kickBanUI = go.GetComponentInChildren<IKickBanUI>();
            kickBanUI.Target = target;
            return kickBanUI;
        }
    }
}
