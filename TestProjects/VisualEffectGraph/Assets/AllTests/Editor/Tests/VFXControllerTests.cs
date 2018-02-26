using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VFX.UI;
using System.IO;
using UnityEngine.TestTools;
using UnityEditor.VFX.Block.Test;

namespace UnityEditor.VFX.Test
{
    [TestFixture]
    public class VFXControllersTests
    {
        VFXViewController m_ViewController;

        const string testAssetName = "Assets/TmpTests/VFXGraph1.asset";

        private int m_StartUndoGroupId;

        [SetUp]
        public void CreateTestAsset()
        {
            VisualEffectAsset asset = new VisualEffectAsset();

            var directoryPath = Path.GetDirectoryName(testAssetName);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            AssetDatabase.CreateAsset(asset, testAssetName);

            m_ViewController = VFXViewController.GetController(asset);
            m_ViewController.useCount++;

            m_StartUndoGroupId = Undo.GetCurrentGroup();
        }

        [TearDown]
        public void DestroyTestAsset()
        {
            m_ViewController.useCount--;
            Undo.RevertAllDownToGroup(m_StartUndoGroupId);
            AssetDatabase.DeleteAsset(testAssetName);
        }

        [Test]
        public void LinkPositionAndDirection()
        {
            var crossDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name.Contains("Cross"));
            var positionDesc = VFXLibrary.GetParameters().FirstOrDefault(o => o.name.Contains("Position"));
            var directionDesc = VFXLibrary.GetParameters().FirstOrDefault(o => o.name.Contains("Direction"));

            var cross = m_ViewController.AddVFXOperator(new Vector2(1, 1), crossDesc);
            var position = m_ViewController.AddVFXParameter(new Vector2(2, 2), positionDesc);
            var direction = m_ViewController.AddVFXParameter(new Vector2(3, 3), directionDesc);

            m_ViewController.ApplyChanges();

            Func<IVFXSlotContainer, VFXNodeController> fnFindController = delegate(IVFXSlotContainer slotContainer)
                {
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.FirstOrDefault(o => o.slotContainer == slotContainer);
                };

            var controllerCross = fnFindController(cross);

            var edgeControllerAppend_A = new VFXDataEdgeController(controllerCross.inputPorts.Where(o => o.portType == typeof(Vector3)).First(), fnFindController(position).outputPorts.First());
            m_ViewController.AddElement(edgeControllerAppend_A);
            m_ViewController.ApplyChanges();

            var edgeControllerAppend_B = new VFXDataEdgeController(controllerCross.inputPorts.Where(o => o.portType == typeof(Vector3)).Last(), fnFindController(direction).outputPorts.First());
            m_ViewController.AddElement(edgeControllerAppend_B);
            m_ViewController.ApplyChanges();

            Assert.AreEqual(1, cross.inputSlots[0].LinkedSlots.Count());
            Assert.AreEqual(1, cross.inputSlots[1].LinkedSlots.Count());

            //TODO : set value & check result => will be a full test
        }

        [Test]
        public void CascadedOperatorAdd()
        {
            Func<IVFXSlotContainer, VFXNodeController> fnFindController = delegate(IVFXSlotContainer slotContainer)
                {
                    var allController = m_ViewController.AllSlotContainerControllers;
                    return allController.FirstOrDefault(o => o.slotContainer == slotContainer);
                };

            var vector2Desc = VFXLibrary.GetParameters().FirstOrDefault(o => o.name == "Vector2");
            var vector2 = m_ViewController.AddVFXParameter(new Vector2(-100, -100), vector2Desc);

            var addDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Add");
            var add = m_ViewController.AddVFXOperator(new Vector2(100, 100), addDesc);

            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var abs = m_ViewController.AddVFXOperator(new Vector2(100, 100), absDesc);

            m_ViewController.ApplyChanges();

            var absController = fnFindController(abs);
            var addController = fnFindController(add);

            var edgeController = new VFXDataEdgeController(absController.inputPorts.First(), addController.outputPorts.First());
            m_ViewController.AddElement(edgeController);
            Assert.AreEqual(VFXValueType.Float, abs.outputSlots[0].GetExpression().valueType);

            var vector2Controller = fnFindController(vector2);
            for (int i = 0; i < 4; ++i)
            {
                edgeController = new VFXDataEdgeController(addController.inputPorts.First(), vector2Controller.outputPorts.First());
                m_ViewController.AddElement(edgeController);
            }

            Assert.AreEqual(VFXValueType.Float2, add.outputSlots[0].GetExpression().valueType);
            Assert.AreEqual(VFXValueType.Float2, abs.outputSlots[0].GetExpression().valueType);

            m_ViewController.RemoveElement(addController);
            Assert.AreEqual(VFXValueType.Float, abs.outputSlots[0].GetExpression().valueType);
        }

        [Test]
        public void AppendOperator()
        {
            Action fnResync = delegate()
                {
                    m_ViewController.ForceReload();
                };

            Func<IVFXSlotContainer, VFXNodeController> fnFindController = delegate(IVFXSlotContainer slotContainer)
                {
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.FirstOrDefault(o => o.slotContainer == slotContainer);
                };

            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var abs = m_ViewController.AddVFXOperator(new Vector2(100, 100), absDesc); fnResync();

            var cosDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Cosine");
            var cos = m_ViewController.AddVFXOperator(new Vector2(200, 100), cosDesc); fnResync();

            var appendDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "AppendVector");
            var append = m_ViewController.AddVFXOperator(new Vector2(300, 100), appendDesc); fnResync();

            var edgeControllerAppend_A = new VFXDataEdgeController(fnFindController(append).inputPorts.First(), fnFindController(abs).outputPorts.First());
            m_ViewController.AddElement(edgeControllerAppend_A); fnResync();

            var edgeControllerCos = new VFXDataEdgeController(fnFindController(cos).inputPorts.First(), fnFindController(append).outputPorts.First());
            m_ViewController.AddElement(edgeControllerCos); fnResync();

            var edgeControllerAppend_B = new VFXDataEdgeController(fnFindController(append).inputPorts[1], fnFindController(abs).outputPorts.First());
            m_ViewController.AddElement(edgeControllerAppend_B); fnResync();

            var edgeControllerAppend_C = new VFXDataEdgeController(fnFindController(append).inputPorts[2], fnFindController(abs).outputPorts.First());
            m_ViewController.AddElement(edgeControllerAppend_C); fnResync();

            var edgeControllerAppend_D = new VFXDataEdgeController(fnFindController(append).inputPorts[3], fnFindController(abs).outputPorts.First());
            m_ViewController.AddElement(edgeControllerAppend_D); fnResync();
        }

        [Test]
        public void UndoRedoCollapseSlot()
        {
            Undo.IncrementCurrentGroup();
            var crossDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name.Contains("Cross"));
            var cross = m_ViewController.AddVFXOperator(new Vector2(0, 0), crossDesc);

            foreach (var slot in cross.inputSlots.Concat(cross.outputSlots))
            {
                Undo.IncrementCurrentGroup();
                Assert.IsTrue(slot.collapsed);
                slot.collapsed = false;
            }

            var totalSlotCount = cross.inputSlots.Concat(cross.outputSlots).Count();
            for (int step = 1; step < totalSlotCount; step++)
            {
                Undo.PerformUndo();
                var vfxOperatorController = m_ViewController.allChildren.OfType<VFXOperatorController>().FirstOrDefault();
                Assert.IsNotNull(vfxOperatorController);

                var slots = vfxOperatorController.Operator.inputSlots.Concat(vfxOperatorController.Operator.outputSlots).Reverse();
                for (int i = 0; i < totalSlotCount; ++i)
                {
                    var slot = slots.ElementAt(i);
                    Assert.AreEqual(i < step, slot.collapsed);
                }
            }

            for (int step = 1; step < totalSlotCount; step++)
            {
                Undo.PerformRedo();
                var vfxOperatorController = m_ViewController.allChildren.OfType<VFXOperatorController>().FirstOrDefault();
                Assert.IsNotNull(vfxOperatorController);

                var slots = vfxOperatorController.Operator.inputSlots.Concat(vfxOperatorController.Operator.outputSlots);
                for (int i = 0; i < totalSlotCount; ++i)
                {
                    var slot = slots.ElementAt(i);
                    Assert.AreEqual(i > step, slot.collapsed);
                }
            }
        }

        [Test]
        public void UndoRedoMoveOperator()
        {
            Undo.IncrementCurrentGroup();
            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var abs = m_ViewController.AddVFXOperator(new Vector2(0, 0), absDesc);

            var positions = new[] { new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 3), new Vector2(4, 4) };
            foreach (var position in positions)
            {
                Undo.IncrementCurrentGroup();
                abs.position = position;
            }

            Func<Type, VFXNodeController> fnFindController = delegate(Type type)
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.FirstOrDefault(o => type.IsInstanceOfType(o.slotContainer));
                };

            for (int i = 0; i < positions.Length; ++i)
            {
                var currentAbs = fnFindController(typeof(VFXOperatorAbsolute));
                Assert.IsNotNull(currentAbs);
                Assert.AreEqual(positions[positions.Length - i - 1].x, currentAbs.model.position.x);
                Assert.AreEqual(positions[positions.Length - i - 1].y, currentAbs.model.position.y);
                Undo.PerformUndo();
            }

            for (int i = 0; i < positions.Length; ++i)
            {
                Undo.PerformRedo();
                var currentAbs = fnFindController(typeof(VFXOperatorAbsolute));
                Assert.IsNotNull(currentAbs);
                Assert.AreEqual(positions[i].x, currentAbs.model.position.x);
                Assert.AreEqual(positions[i].y, currentAbs.model.position.y);
            }
        }

        [Test]
        public void UndoRedoAddOperator()
        {
            Func<VFXNodeController[]> fnAllOperatorController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.OfType<VFXOperatorController>().ToArray();
                };


            Action fnTestShouldExist = delegate()
                {
                    var allOperatorController = fnAllOperatorController();
                    Assert.AreEqual(1, allOperatorController.Length);
                    Assert.IsInstanceOf(typeof(VFXOperatorAbsolute), allOperatorController[0].model);
                };

            Action fnTestShouldNotExist = delegate()
                {
                    var allOperatorController = fnAllOperatorController();
                    Assert.AreEqual(0, allOperatorController.Length);
                };

            Undo.IncrementCurrentGroup();
            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var abs = m_ViewController.AddVFXOperator(new Vector2(0, 0), absDesc);

            fnTestShouldExist();
            Undo.PerformUndo();
            fnTestShouldNotExist();
            Undo.PerformRedo();
            fnTestShouldExist();

            Undo.IncrementCurrentGroup();
            m_ViewController.RemoveElement(fnAllOperatorController()[0]);
            fnTestShouldNotExist();
            Undo.PerformUndo();
            fnTestShouldExist();
            Undo.PerformRedo();
            fnTestShouldNotExist();
        }

        [Test]
        public void UndoRedoSetSlotValue()
        {
            Func<VFXNodeController[]> fnAllOperatorController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.OfType<VFXOperatorController>().ToArray();
                };

            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var abs = m_ViewController.AddVFXOperator(new Vector2(0, 0), absDesc);

            var absOperator = fnAllOperatorController()[0];

            Undo.IncrementCurrentGroup();
            absOperator.inputPorts[0].value = 0;
            Undo.IncrementCurrentGroup();

            absOperator.inputPorts[0].value = 123;

            Undo.PerformUndo();

            Assert.AreEqual(0, absOperator.inputPorts[0].value);

            Undo.PerformRedo();

            Assert.AreEqual(123, absOperator.inputPorts[0].value);
        }

        [Test]
        public void UndoRedoSetSlotValueThenGraphChange()
        {
            Func<VFXNodeController[]> fnAllOperatorController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.OfType<VFXOperatorController>().ToArray();
                };

            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var abs = m_ViewController.AddVFXOperator(new Vector2(0, 0), absDesc);

            var absOperator = fnAllOperatorController()[0];

            Undo.IncrementCurrentGroup();
            absOperator.inputPorts[0].value = 0;

            absOperator.position = new Vector2(1, 2);


            Undo.IncrementCurrentGroup();

            absOperator.inputPorts[0].value = 123;

            Undo.IncrementCurrentGroup();

            absOperator.position = new Vector2(123, 456);

            Undo.IncrementCurrentGroup();

            absOperator.inputPorts[0].value = 789;

            Undo.PerformUndo(); // this should undo value change only

            Assert.AreEqual(123, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(123, 456), absOperator.position);

            Undo.PerformUndo(); // this should undo position change only

            Assert.AreEqual(123, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(1, 2), absOperator.position);

            Undo.PerformUndo(); // this should undo value change only

            Assert.AreEqual(0, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(1, 2), absOperator.position);

            Undo.PerformRedo(); // this should redo value change only

            Assert.AreEqual(123, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(1, 2), absOperator.position);

            Undo.PerformRedo(); // this should redo position change only

            Assert.AreEqual(123, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(123, 456), absOperator.position);

            Undo.PerformRedo(); // this should redo value change only

            Assert.AreEqual(789, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(123, 456), absOperator.position);
        }

        [Test]
        public void UndoRedoSetSlotValueAndGraphChange()
        {
            Func<VFXNodeController[]> fnAllOperatorController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.OfType<VFXOperatorController>().ToArray();
                };

            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var abs = m_ViewController.AddVFXOperator(new Vector2(0, 0), absDesc);

            var absOperator = fnAllOperatorController()[0];

            Undo.IncrementCurrentGroup();
            absOperator.inputPorts[0].value = 0;

            absOperator.position = new Vector2(1, 2);


            Undo.IncrementCurrentGroup();

            absOperator.inputPorts[0].value = 123;
            absOperator.position = new Vector2(123, 456);

            Undo.PerformUndo();

            Assert.AreEqual(123, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(1, 2), absOperator.position);

            Undo.PerformRedo();

            Assert.AreEqual(123, absOperator.inputPorts[0].value);
            Assert.AreEqual(new Vector2(123, 456), absOperator.position);
        }

        [Test]
        public void UndoRedoOperatorLinkSimple()
        {
            Func<Type, VFXNodeController> fnFindController = delegate(Type type)
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.FirstOrDefault(o => type.IsInstanceOfType(o.slotContainer));
                };

            var cosDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Cosine");
            var sinDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Sine");
            Undo.IncrementCurrentGroup();
            var cos = m_ViewController.AddVFXOperator(new Vector2(0, 0), cosDesc);
            Undo.IncrementCurrentGroup();
            var sin = m_ViewController.AddVFXOperator(new Vector2(1, 1), sinDesc);
            var cosController = fnFindController(typeof(VFXOperatorCosine));
            var sinController = fnFindController(typeof(VFXOperatorSine));

            Func<int> fnCountEdge = delegate()
                {
                    return m_ViewController.allChildren.OfType<VFXDataEdgeController>().Count();
                };

            Undo.IncrementCurrentGroup();
            Assert.AreEqual(0, fnCountEdge());

            var edgeControllerSin = new VFXDataEdgeController(sinController.inputPorts[0], cosController.outputPorts[0]);
            m_ViewController.AddElement(edgeControllerSin);
            Assert.AreEqual(1, fnCountEdge());

            Undo.PerformUndo();
            Assert.AreEqual(0, fnCountEdge());
            Assert.NotNull(fnFindController(typeof(VFXOperatorCosine)));
            Assert.NotNull(fnFindController(typeof(VFXOperatorSine)));
        }

        [Test]
        public void UndoRedoOperatorLinkToBlock()
        {
            Func<VFXContextController> fnFirstContextController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXContextController>().FirstOrDefault();
                };

            Func<Type, VFXNodeController> fnFindController = delegate(Type type)
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.FirstOrDefault(o => type.IsInstanceOfType(o.slotContainer));
                };

            Func<VFXBlockController> fnFirstBlockController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXContextController>().SelectMany(t => t.blockControllers).FirstOrDefault();
                };

            Func<VFXDataEdgeController> fnFirstEdgeController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXDataEdgeController>().FirstOrDefault();
                };

            Undo.IncrementCurrentGroup();
            var cosDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Cosine");
            var contextUpdateDesc = VFXLibrary.GetContexts().FirstOrDefault(o => o.name.Contains("Update"));
            var blockAttributeDesc = VFXLibrary.GetBlocks().FirstOrDefault(o => o.modelType == typeof(Block.SetAttribute));

            var cos = m_ViewController.AddVFXOperator(new Vector2(0, 0), cosDesc);
            var update = m_ViewController.AddVFXContext(new Vector2(2, 2), contextUpdateDesc);
            var blockAttribute = blockAttributeDesc.CreateInstance();
            blockAttribute.SetSettingValue("attribute", "color");
            fnFirstContextController().AddBlock(0, blockAttribute);

            var edgeController = new VFXDataEdgeController(fnFirstBlockController().inputPorts[0], fnFindController(typeof(VFXOperatorCosine)).outputPorts[0]);
            m_ViewController.AddElement(edgeController);
            Undo.IncrementCurrentGroup();

            m_ViewController.RemoveElement(fnFirstEdgeController());
            Assert.IsNull(fnFirstEdgeController());
            Undo.IncrementCurrentGroup();

            Undo.PerformUndo();
            Assert.IsNotNull(fnFirstEdgeController());

            Undo.PerformRedo();
            Assert.IsNull(fnFirstEdgeController());
        }

        [Test]
        public void UndoRedoOperatorLinkAdvanced()
        {
            Func<Type, VFXNodeController> fnFindController = delegate(Type type)
                {
                    m_ViewController.ApplyChanges();
                    var allController = m_ViewController.allChildren.OfType<VFXNodeController>();
                    return allController.FirstOrDefault(o => type.IsInstanceOfType(o.slotContainer));
                };

            Func<int> fnCountEdge = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXDataEdgeController>().Count();
                };


            var absDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Absolute");
            var appendDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "AppendVector");
            var crossDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Cross Product");
            var cosDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Cosine");
            var sinDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "Sine");

            var abs = m_ViewController.AddVFXOperator(new Vector2(0, 0), absDesc);
            var append = m_ViewController.AddVFXOperator(new Vector2(1, 1), appendDesc);
            var cross = m_ViewController.AddVFXOperator(new Vector2(2, 2), crossDesc);
            var cos = m_ViewController.AddVFXOperator(new Vector2(3, 3), cosDesc);
            var sin = m_ViewController.AddVFXOperator(new Vector2(4, 4), sinDesc);

            var absController = fnFindController(typeof(VFXOperatorAbsolute));
            var appendController = fnFindController(typeof(VFXOperatorAppendVector));
            var crossController = fnFindController(typeof(VFXOperatorCrossProduct));

            for (int i = 0; i < 3; ++i)
            {
                var edgeController = new VFXDataEdgeController(appendController.inputPorts[i], absController.outputPorts[0]);
                m_ViewController.AddElement(edgeController);
                m_ViewController.ApplyChanges();
            }

            var edgeControllerCross = new VFXDataEdgeController(crossController.inputPorts[0], appendController.outputPorts[0]);
            m_ViewController.AddElement(edgeControllerCross);

            Undo.IncrementCurrentGroup();
            Assert.AreEqual(4, fnCountEdge());
            Assert.IsInstanceOf(typeof(VFXSlotFloat3), (appendController.outputPorts[0] as VFXDataAnchorController).model);

            //Find last edge in append node
            var referenceAnchor = appendController.inputPorts[2];
            m_ViewController.ApplyChanges();
            var edgeToDelete = m_ViewController.allChildren
                .OfType<VFXDataEdgeController>()
                .Cast<VFXDataEdgeController>()
                .FirstOrDefault(e =>
                {
                    return e.input == referenceAnchor;
                });
            Assert.NotNull(edgeToDelete);

            m_ViewController.RemoveElement(edgeToDelete);
            Assert.AreEqual(2, fnCountEdge()); //cross should be implicitly disconnected ...
            Assert.IsInstanceOf(typeof(VFXSlotFloat2), (appendController.outputPorts[0] as VFXDataAnchorController).model);

            Undo.PerformUndo();
            Assert.AreEqual(4, fnCountEdge()); //... and restored !
            Assert.IsInstanceOf(typeof(VFXSlotFloat3), (fnFindController(typeof(VFXOperatorAppendVector)).outputPorts[0] as VFXDataAnchorController).model);
            Undo.PerformRedo();
            Assert.AreEqual(2, fnCountEdge());
            Assert.IsInstanceOf(typeof(VFXSlotFloat2), (fnFindController(typeof(VFXOperatorAppendVector)).outputPorts[0] as VFXDataAnchorController).model);

            //Improve test connecting cos & sin => then try delete append
            Undo.PerformUndo();
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(4, fnCountEdge());
            Assert.IsInstanceOf(typeof(VFXSlotFloat3), (fnFindController(typeof(VFXOperatorAppendVector)).outputPorts[0] as VFXDataAnchorController).model);

            var edgeControllerCos = new VFXDataEdgeController(fnFindController(typeof(VFXOperatorCosine)).inputPorts[0], fnFindController(typeof(VFXOperatorAppendVector)).outputPorts[0]);
            m_ViewController.AddElement(edgeControllerCos);
            Assert.AreEqual(5, fnCountEdge());

            var edgeControllerSin = new VFXDataEdgeController(fnFindController(typeof(VFXOperatorSine)).inputPorts[0], fnFindController(typeof(VFXOperatorAppendVector)).outputPorts[0]);
            m_ViewController.AddElement(edgeControllerSin);
            Assert.AreEqual(6, fnCountEdge());

            m_ViewController.RemoveElement(fnFindController(typeof(VFXOperatorAppendVector)));
            Assert.AreEqual(0, fnCountEdge());
        }

        [Test]
        public void UndoRedoOperatorSettings()
        {
            Func<VFXOperatorController> fnFirstOperatorController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXOperatorController>().FirstOrDefault();
                };

            Action<VFXOperatorComponentMask, string> fnSetSetting = delegate(VFXOperatorComponentMask target, string mask)
                {
                    target.x = target.y = target.z = target.w = VFXOperatorComponentMask.Component.None;
                    for (int i = 0; i < mask.Length; ++i)
                    {
                        var current = (VFXOperatorComponentMask.Component)Enum.Parse(typeof(VFXOperatorComponentMask.Component),  mask[i].ToString().ToUpper());
                        if (i == 0)
                        {
                            target.x = current;
                        }
                        else if (i == 1)
                        {
                            target.y = current;
                        }
                        else if (i == 2)
                        {
                            target.z = current;
                        }
                        else if (i == 3)
                        {
                            target.w = current;
                        }
                    }
                    target.Invalidate(VFXModel.InvalidationCause.kSettingChanged);
                };

            Func<VFXOperatorComponentMask, string> fnGetSetting = delegate(VFXOperatorComponentMask target)
                {
                    var value = "";
                    if (target.x != VFXOperatorComponentMask.Component.None)
                        value += target.x.ToString().ToLower();
                    if (target.y != VFXOperatorComponentMask.Component.None)
                        value += target.y.ToString().ToLower();
                    if (target.z != VFXOperatorComponentMask.Component.None)
                        value += target.z.ToString().ToLower();
                    if (target.w != VFXOperatorComponentMask.Component.None)
                        value += target.w.ToString().ToLower();
                    return value;
                };

            var componentMaskDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name == "ComponentMask");
            var componentMask = m_ViewController.AddVFXOperator(new Vector2(0, 0), componentMaskDesc);

            var maskList = new string[] { "xy", "yww", "xw", "z" };
            for (int i = 0; i < maskList.Length; ++i)
            {
                var componentMaskController = fnFirstOperatorController();
                Undo.IncrementCurrentGroup();
                fnSetSetting(componentMaskController.model as VFXOperatorComponentMask, maskList[i]);
                Assert.AreEqual(maskList[i], fnGetSetting(componentMaskController.model as VFXOperatorComponentMask));
            }

            for (int i = maskList.Length - 1; i > 0; --i)
            {
                Undo.PerformUndo();
                var componentMaskController = fnFirstOperatorController();
                Assert.AreEqual(maskList[i - 1], fnGetSetting(componentMaskController.model as VFXOperatorComponentMask));
            }

            for (int i = 0; i < maskList.Length - 1; ++i)
            {
                Undo.PerformRedo();
                var componentMaskController = fnFirstOperatorController();
                Assert.AreEqual(maskList[i + 1], fnGetSetting(componentMaskController.model as VFXOperatorComponentMask));
            }
        }

        [Test]
        public void UndoRedoAddBlockContext()
        {
            var contextUpdateDesc = VFXLibrary.GetContexts().FirstOrDefault(o => o.name.Contains("Update"));
            var blockDesc = VFXLibrary.GetBlocks().FirstOrDefault(o => o.modelType == typeof(AllType));

            var contextUpdate = m_ViewController.AddVFXContext(Vector2.one, contextUpdateDesc);
            Func<VFXContextController> fnContextController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    var allContextController = m_ViewController.allChildren.OfType<VFXContextController>().ToArray();
                    return allContextController.FirstOrDefault() as VFXContextController;
                };
            Assert.IsNotNull(fnContextController());
            //Creation
            Undo.IncrementCurrentGroup();
            fnContextController().AddBlock(0, blockDesc.CreateInstance());
            Assert.AreEqual(1, fnContextController().context.children.Count());
            Undo.PerformUndo();
            Assert.AreEqual(0, fnContextController().context.children.Count());

            //Deletion
            var block = blockDesc.CreateInstance();
            fnContextController().AddBlock(0, block);
            Assert.AreEqual(1, fnContextController().context.children.Count());
            Undo.IncrementCurrentGroup();
            fnContextController().RemoveBlock(block);
            Assert.AreEqual(0, fnContextController().context.children.Count());

            Undo.PerformUndo();

            m_ViewController.ApplyChanges();


            Assert.IsNotNull(fnContextController());
            Assert.AreEqual(1, fnContextController().context.children.Count());
            Assert.IsInstanceOf(typeof(AllType), fnContextController().context.children.First());
        }

        [Test]
        public void UndoRedoContext()
        {
            Func<VFXContextController> fnFirstContextController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXContextController>().FirstOrDefault() as VFXContextController;
                };

            var contextDesc = VFXLibrary.GetContexts().FirstOrDefault();
            Undo.IncrementCurrentGroup();
            m_ViewController.AddVFXContext(Vector2.zero, contextDesc);

            Assert.NotNull(fnFirstContextController());
            Undo.PerformUndo();
            Assert.Null(fnFirstContextController(), "Fail Undo Create");

            Undo.IncrementCurrentGroup();
            m_ViewController.AddVFXContext(Vector2.zero, contextDesc);
            Assert.NotNull(fnFirstContextController());

            Undo.IncrementCurrentGroup();
            m_ViewController.RemoveElement(fnFirstContextController());
            Assert.Null(fnFirstContextController());

            Undo.PerformUndo();
            Assert.NotNull(fnFirstContextController(), "Fail Undo Delete");
        }

        [Test]
        public void UndoRedoContextLinkMultiSlot()
        {
            Func<VFXContextController[]> fnContextController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXContextController>().Cast<VFXContextController>().ToArray();
                };

            Func<VFXContextController> fnSpawner = delegate()
                {
                    var controller = fnContextController();
                    return controller.FirstOrDefault(o => o.model.name.Contains("Spawner"));
                };

            Func<string, VFXContextController> fnEvent = delegate(string name)
                {
                    var controller = fnContextController();
                    var allEvent = controller.Where(o => o.model.name.Contains("Event"));
                    return allEvent.FirstOrDefault(o => (o.model as VFXBasicEvent).eventName == name) as VFXContextController;
                };

            Func<VFXContextController> fnStart = delegate()
                {
                    return fnEvent("Start");
                };

            Func<VFXContextController> fnStop = delegate()
                {
                    return fnEvent("Stop");
                };

            Func<int> fnFlowEdgeCount = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXFlowEdgeController>().Count();
                };

            var contextSpawner = VFXLibrary.GetContexts().FirstOrDefault(o => o.name.Contains("Spawner"));
            var contextEvent = VFXLibrary.GetContexts().FirstOrDefault(o => o.name.Contains("Event"));

            m_ViewController.AddVFXContext(new Vector2(1, 1), contextSpawner);
            var eventStartController = m_ViewController.AddVFXContext(new Vector2(2, 2), contextEvent) as VFXBasicEvent;
            var eventStopController = m_ViewController.AddVFXContext(new Vector2(3, 3), contextEvent) as VFXBasicEvent;
            eventStartController.SetSettingValue("eventName", "Start");
            eventStopController.SetSettingValue("eventName", "Stop");

            //Creation
            var flowEdge = new VFXFlowEdgeController(fnSpawner().flowInputAnchors.ElementAt(0), fnStart().flowOutputAnchors.FirstOrDefault());

            Undo.IncrementCurrentGroup();
            m_ViewController.AddElement(flowEdge);
            Assert.AreEqual(1, fnFlowEdgeCount());

            flowEdge = new VFXFlowEdgeController(fnSpawner().flowInputAnchors.ElementAt(1), fnStop().flowOutputAnchors.FirstOrDefault());

            Undo.IncrementCurrentGroup();
            m_ViewController.AddElement(flowEdge);
            Assert.AreEqual(2, fnFlowEdgeCount());

            //Test a single deletion
            var allFlowEdge = m_ViewController.allChildren.OfType<VFXFlowEdgeController>().ToArray();

            // Integrity test...
            var inputSlotIndex = allFlowEdge.Select(o => (o.input as VFXFlowAnchorController).slotIndex).OrderBy(o => o).ToArray();
            var outputSlotIndex = allFlowEdge.Select(o => (o.output as VFXFlowAnchorController).slotIndex).OrderBy(o => o).ToArray();

            Assert.AreEqual(inputSlotIndex[0], 0);
            Assert.AreEqual(inputSlotIndex[1], 1);
            Assert.AreEqual(outputSlotIndex[0], 0);
            Assert.AreEqual(outputSlotIndex[1], 0);

            var edge = allFlowEdge.First(o => o.input == fnSpawner().flowInputAnchors.ElementAt(1) && o.output == fnStop().flowOutputAnchors.FirstOrDefault());

            Undo.IncrementCurrentGroup();
            m_ViewController.RemoveElement(edge);
            Assert.AreEqual(1, fnFlowEdgeCount());

            Undo.PerformUndo();
            Assert.AreEqual(2, fnFlowEdgeCount());

            Undo.PerformRedo();
            Assert.AreEqual(1, fnFlowEdgeCount());

            Undo.PerformUndo();
            Assert.AreEqual(2, fnFlowEdgeCount());

            //Global Deletion
            Undo.PerformUndo();
            Assert.AreEqual(1, fnFlowEdgeCount());

            Undo.PerformUndo();
            Assert.AreEqual(0, fnFlowEdgeCount());
        }

        [Test]
        public void UndoRedoContextLink()
        {
            Func<VFXContextController[]> fnContextController = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXContextController>().Cast<VFXContextController>().ToArray();
                };

            Func<VFXContextController> fnInitializeController = delegate()
                {
                    var controller = fnContextController();
                    return controller.FirstOrDefault(o => o.model.name.Contains("Init"));
                };

            Func<VFXContextController> fnUpdateController = delegate()
                {
                    var controller = fnContextController();
                    return controller.FirstOrDefault(o => o.model.name.Contains("Update"));
                };

            Func<int> fnFlowEdgeCount = delegate()
                {
                    m_ViewController.ApplyChanges();
                    return m_ViewController.allChildren.OfType<VFXFlowEdgeController>().Count();
                };

            var contextInitializeDesc = VFXLibrary.GetContexts().FirstOrDefault(o => o.name.Contains("Init"));
            var contextUpdateDesc = VFXLibrary.GetContexts().FirstOrDefault(o => o.name.Contains("Update"));

            var contextInitialize = m_ViewController.AddVFXContext(new Vector2(1, 1), contextInitializeDesc);
            var contextUpdate = m_ViewController.AddVFXContext(new Vector2(2, 2), contextUpdateDesc);

            //Creation
            var flowEdge = new VFXFlowEdgeController(fnUpdateController().flowInputAnchors.FirstOrDefault(), fnInitializeController().flowOutputAnchors.FirstOrDefault());

            Undo.IncrementCurrentGroup();
            m_ViewController.AddElement(flowEdge);
            Assert.AreEqual(1, fnFlowEdgeCount());

            Undo.PerformUndo();
            Assert.AreEqual(0, fnFlowEdgeCount(), "Fail undo Create");

            //Deletion
            flowEdge = new VFXFlowEdgeController(fnUpdateController().flowInputAnchors.FirstOrDefault(), fnInitializeController().flowOutputAnchors.FirstOrDefault());
            m_ViewController.AddElement(flowEdge);
            Assert.AreEqual(1, fnFlowEdgeCount());

            Undo.IncrementCurrentGroup();
            m_ViewController.RemoveElement(m_ViewController.allChildren.OfType<VFXFlowEdgeController>().FirstOrDefault());
            Assert.AreEqual(0, fnFlowEdgeCount());

            Undo.PerformUndo();
            Assert.AreEqual(1, fnFlowEdgeCount(), "Fail undo Delete");
        }

        [Test]
        public void DeleteSubSlotWithLink()
        {
            var crossProductDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name.Contains("Cross"));
            var sinDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name.Contains("Sin"));
            var cosDesc = VFXLibrary.GetOperators().FirstOrDefault(o => o.name.Contains("Cos"));

            var crossProduct = m_ViewController.AddVFXOperator(new Vector2(0, 0), crossProductDesc);
            var sin = m_ViewController.AddVFXOperator(new Vector2(8, 8), sinDesc);
            var cos = m_ViewController.AddVFXOperator(new Vector2(-8, 8), cosDesc);

            m_ViewController.ApplyChanges();

            crossProduct.outputSlots[0].children.ElementAt(1).Link(sin.inputSlots[0]);
            crossProduct.outputSlots[0].children.ElementAt(1).Link(cos.inputSlots[0]);

            var crossController = m_ViewController.allChildren.OfType<VFXOperatorController>().First(o => o.model.name.Contains("Cross"));
            m_ViewController.RemoveElement(crossController);

            Assert.IsFalse(cos.inputSlots[0].HasLink(true));
            Assert.IsFalse(sin.inputSlots[0].HasLink(true));
        }
    }
}
