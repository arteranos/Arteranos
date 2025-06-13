/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Arteranos.Core;
using UnityEngine.Events;

using System.Reflection;
using Arteranos.Common;

namespace Arteranos.UI
{
    public class ContentFilterUI : ActionPage
    {
        internal struct ContentFilterEntry
        {
            public string Name;
            public string FieldName;
            public string Description;

            public ContentFilterEntry(string _Name, string _FieldName, string _Description)
            {
                Name = _Name;
                FieldName = _FieldName;
                Description = _Description;
            }
        };

        public Transform tbl_Table;
        public TextMeshProUGUI lbl_HelpText;

        private PermissionsJSON spj = null;

        // private readonly List<Button> btns_Help = new();
        // private readonly List<Spinner> spns_Config = new();

        internal readonly ContentFilterEntry[] Filters = new ContentFilterEntry[]
        {
            new("Nudity",
                "Nudity",
                "<b>Nudity</b>\n" +
                    "Nudity, non-sexual or artistic."
                ),
            new("Suggestive",
                "Suggestive",
                "<b>(sexually) suggestive</b>\n" +
                    "Not including nudity, but characters depicted in the content look like " +
                    "or behave with the flirtatious actions or in an outright " +
                    "sexual manner."
                ),
            new("Violence",
                "Violence",
                "<b>Cartoon / \"Clean\" violence</b>\n" +
                    "Violence, but no depictions of lasting harm, no blood, no gore."
                ),
            new("Explicit Nudity",
                "ExplicitNudes",
                "<b>Explicit Nudity</b>\n" +
                    "Detailed depiction of sexual acts or derivation thereof, " +
                    "be it still or animated.\n" +
                    "<color=#ff8888><b>Not for children at all!</b></color>"
                ),
            new("Excessive Violence",
                "ExcessiveViolence",
                "<b>Realistic/Excessive violence</b>\n" +
                    "1. Violence, depicted in a realistic manner. Blood and/or gore.\n" +
                    "2. Excessive violence. Self-harm, or torture.\n" +
                    "<color=#ff8888><b>Not for children at all!</b></color>"
                )
        };
    
        protected override void Awake()
        {
            string[] spnOptions = new string[]
            {
                "Forbidden",
                "Uncertain",
                "Allowed"
            };

            base.Awake();

            UnityAction makeShowHelpText(int row) =>
                () => lbl_HelpText.text = Filters[row].Description;

            Action<int, bool> makeOnSpinnerClicked(int row) =>
                (val, up) => lbl_HelpText.text = string.Empty;

            // Take the first (and only) row as a blueprint for the remaining rows
            Transform DescColumn = tbl_Table.GetChild(0);
            Transform HelpColumn = tbl_Table.GetChild(1);
            Transform ConfigColumn = tbl_Table.GetChild(2);

            TextMeshProUGUI lbl_tmpl = DescColumn.GetChild(0).GetComponent<TextMeshProUGUI>();
            Button btn_tmpl = HelpColumn.GetChild(0).GetComponent<Button>();
            Spinner spn_tmpl = ConfigColumn.GetChild(0).GetComponent<Spinner>();

            for(int i = 0, c = Filters.Length;i < c;i++)
            {
                TextMeshProUGUI lbl = lbl_tmpl;
                Button btn = btn_tmpl;
                Spinner spn = spn_tmpl;

                if(i > 0)
                {
                    lbl = Instantiate(lbl_tmpl, DescColumn, false);
                    btn = Instantiate(btn_tmpl, HelpColumn, false);
                    spn = Instantiate(spn_tmpl, ConfigColumn, false);
                }

                lbl.text = Filters[i].Name;

                btn.onClick.AddListener(makeShowHelpText(i));

                spn.OnChanged += makeOnSpinnerClicked(i);
                spn.Options = spnOptions;
            }

            lbl_HelpText.text = string.Empty;
        }

        bool? Spn2bool(Spinner spn)
        {
            return spn.value switch
            {
                0 => false,
                2 => true,
                _ => null,
            };
        }

        int Bool2spn(bool? b)
        {
            return b switch
            {
                false => 0,
                true => 2,
                _ => 1,
            };
        }

        private readonly Dictionary<string, PropertyInfo> spjfields = new();

        public override void Called(object data)
        {
            base.Called(data);

            spj = data as PermissionsJSON;

            Debug.Assert(spj != null, "No association of the Content Filter UI");

            PropertyInfo[] fields = spj.GetType().GetProperties();
            foreach (PropertyInfo field in fields) spjfields[field.Name] = field;

            Transform ConfigColumn = tbl_Table.GetChild(2);

            for (int i = 0, c = Filters.Length; i < c; ++i)
            {
                Spinner spn = ConfigColumn.GetChild(i).GetComponent<Spinner>();
                spn.value = Bool2spn(spjfields[Filters[i].FieldName].GetValue(spj) as bool?);
            }
        }

        public override void BackingOut(ref object result)
        {
            Transform ConfigColumn = tbl_Table.GetChild(2);

            for (int i = 0, c = Filters.Length; i < c; ++i)
            {
                Spinner spn = ConfigColumn.GetChild(i).GetComponent<Spinner>();
                spjfields[Filters[i].FieldName].SetValue(spj, Spn2bool(spn));
            }

            result = spj;
            base.BackingOut(ref result);
        }
    }
}
