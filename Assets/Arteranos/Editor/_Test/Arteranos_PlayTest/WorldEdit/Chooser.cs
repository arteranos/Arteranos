/*
 *Copyright(c) 2024, willneedit
 *
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;


using Arteranos.WorldEdit;
using System.IO;
using System;
using Object = UnityEngine.Object;

namespace Arteranos.PlayTest.WorldEdit
{
    public class Chooser : WorldEditFixture
    {
        private GameObject canvas = null;
        private GameObject editorUI = null;
        private Transform ItemContainer = null;

        WorldObject wob = null;

        [UnitySetUp]
        public IEnumerator SetUp1()
        {
            wob = BuildSample();

            yield return wob.Instantiate(pl.transform);

            yield return ShowPanel();
        }


        [UnityTearDown]
        public IEnumerator TearDown1()
        {
            yield return null;

            Object.Destroy(canvas);
        }

        public IEnumerator ShowPanel()
        {
            GameObject canvasBlueprint = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Arteranos/Editor/_Test/Canvas_Preferences_Edit.prefab");
            canvas = Object.Instantiate(canvasBlueprint);

            GameObject editorUIBlueprint = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Arteranos/Modules/WorldEdit/UI/WorldEditorUI.prefab");
            editorUI = Object.Instantiate(editorUIBlueprint, canvas.transform, false);

            WorldObjectList list = editorUI.GetComponent<WorldEditorUI>().ObjectList;

            ItemContainer = list.transform.GetChild(1);

            Assert.IsNotNull(ItemContainer);
            Assert.AreEqual("ObjectGallery", ItemContainer.name);

            yield return null;
        }

        public WorldObjectListItem GetWOChooserItem(int index)
        {
            Transform panels = ItemContainer;

            GameObject go = index < panels.childCount
                ? panels.GetChild(index).gameObject
                : null;

            return go != null ? go.GetComponent<WorldObjectListItem>() : null;
        }

        [UnityTest]
        public IEnumerator T001_Show()
        {
            Assert.AreEqual(2, ItemContainer.childCount);
            Assert.AreEqual("(Root)", GetWOChooserItem(0).txt_Name.text);
            Assert.AreEqual("Test Cube", GetWOChooserItem(1).txt_Name.text);
            Assert.False(GetWOChooserItem(0).btn_ToParent.isActiveAndEnabled); // Item 0 is the root
            Assert.False(GetWOChooserItem(1).btn_ToParent.isActiveAndEnabled); // Item 1 and more

            Assert.False(GetWOChooserItem(0).btn_ToChild.isActiveAndEnabled);  // Item 0 is just the heading
            Assert.True(GetWOChooserItem(1).btn_ToChild.isActiveAndEnabled);   // Item 1 does have children

            yield return new WaitForEndOfFrame();
        }

        [UnityTest]
        public IEnumerator T002_Descend() 
        {
            GetWOChooserItem(1).Test_OnToChildClicked();     // Select first child, down a level
            yield return new WaitForEndOfFrame();

            Assert.AreEqual(3, ItemContainer.childCount);
            Assert.AreEqual("Test Cube", GetWOChooserItem(0).txt_Name.text);
            Assert.AreEqual("Test Sphere", GetWOChooserItem(1).txt_Name.text);
            Assert.AreEqual("Test Cube Right", GetWOChooserItem(2).txt_Name.text);

            Assert.True(GetWOChooserItem(0).btn_ToParent.isActiveAndEnabled);
            Assert.False(GetWOChooserItem(1).btn_ToParent.isActiveAndEnabled);
            Assert.False(GetWOChooserItem(2).btn_ToParent.isActiveAndEnabled);

            Assert.False(GetWOChooserItem(0).btn_ToChild.isActiveAndEnabled);
            Assert.True(GetWOChooserItem(1).btn_ToChild.isActiveAndEnabled);    // Sphere has the Capsule as child
            Assert.True(GetWOChooserItem(2).btn_ToChild.isActiveAndEnabled);    // Right Cube has no children, but allow to add a new leaf

            GetWOChooserItem(0).Test_OnToParentClicked();    // Select parent link, up a level
            yield return new WaitForEndOfFrame();

            Assert.AreEqual(2, ItemContainer.childCount);
            Assert.AreEqual("(Root)", GetWOChooserItem(0).txt_Name.text);
            Assert.AreEqual("Test Cube", GetWOChooserItem(1).txt_Name.text);
        }

        [UnityTest]
        public IEnumerator T003_Lock_Unlock()
        {
            // Initial state
            Assert.True(GetWOChooserItem(1).btn_Property.interactable);
            Assert.True(GetWOChooserItem(1).txt_Name.interactable);
            Assert.True(GetWOChooserItem(1).btn_Delete.interactable);
            Assert.True(GetWOChooserItem(1).btn_Lock.isActiveAndEnabled);
            Assert.False(GetWOChooserItem(1).btn_Unlock.isActiveAndEnabled);

            GetWOChooserItem(1).Test_OnSetLockState(true); 
            yield return new WaitForEndOfFrame();

            // Locked - no modifying or deletion
            Assert.False(GetWOChooserItem(1).btn_Property.interactable);
            Assert.False(GetWOChooserItem(1).txt_Name.interactable);
            Assert.False(GetWOChooserItem(1).btn_Delete.interactable);
            Assert.False(GetWOChooserItem(1).btn_Lock.isActiveAndEnabled);
            Assert.True(GetWOChooserItem(1).btn_Unlock.isActiveAndEnabled);

            GetWOChooserItem(1).Test_OnSetLockState(false);
            yield return new WaitForEndOfFrame();

            // Unlocked - just as before
            Assert.True(GetWOChooserItem(1).btn_Property.interactable);
            Assert.True(GetWOChooserItem(1).txt_Name.interactable);
            Assert.True(GetWOChooserItem(1).btn_Delete.interactable);
            Assert.True(GetWOChooserItem(1).btn_Lock.isActiveAndEnabled);
            Assert.False(GetWOChooserItem(1).btn_Unlock.isActiveAndEnabled);
        }

        [UnityTest]
        public IEnumerator T004_Delete()
        {
            GetWOChooserItem(1).Test_OnToChildClicked();     // Select first child, down a level
            yield return new WaitForEndOfFrame();

            Assert.AreEqual(3, ItemContainer.childCount);
            Assert.AreEqual("Test Cube", GetWOChooserItem(0).txt_Name.text);
            Assert.AreEqual("Test Sphere", GetWOChooserItem(1).txt_Name.text);
            Assert.AreEqual("Test Cube Right", GetWOChooserItem(2).txt_Name.text);

            GetWOChooserItem(1).Test_OnDeleteClicked();             // Delete the sphere _and its children_
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            Assert.AreEqual(2, ItemContainer.childCount);
            Assert.AreEqual("Test Cube", GetWOChooserItem(0).txt_Name.text);
            Assert.AreEqual("Test Cube Right", GetWOChooserItem(1).txt_Name.text);
        }
    }
}