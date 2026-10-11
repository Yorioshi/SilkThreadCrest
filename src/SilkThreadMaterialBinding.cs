using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilkThreadCrest
{
    // tk2dSprite.UpdateMaterial reads collectionInst.spriteDefinitions[id].materialInst.
    // Updating just collection.materials/materialInsts is not sufficient.
    internal static class SilkThreadMaterialBinding
    {
        private static readonly FieldInfo RuntimeCollectionField = typeof(tk2dBaseSprite).GetField(
            "collectionInst", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        internal sealed class FrameSnapshot
        {
            internal tk2dSpriteDefinition Definition;
            internal Material Material;
            internal Material MaterialInst;
        }

        internal static tk2dSpriteCollectionData RuntimeCollection(tk2dBaseSprite sprite)
        {
            if (!sprite)
                return null;
            var runtime = RuntimeCollectionField != null
                ? RuntimeCollectionField.GetValue(sprite) as tk2dSpriteCollectionData
                : null;
            if (runtime)
                return runtime;
            return sprite.Collection ? sprite.Collection.inst : null;
        }

        internal static List<FrameSnapshot> Capture(tk2dSpriteCollectionData collection)
        {
            var result = new List<FrameSnapshot>();
            if (!collection || collection.spriteDefinitions == null)
                return result;
            foreach (var definition in collection.spriteDefinitions)
            {
                if (definition == null)
                    continue;
                result.Add(new FrameSnapshot {
                    Definition = definition, Material = definition.material,
                    MaterialInst = definition.materialInst
                });
            }
            return result;
        }

        internal static int Bind(tk2dSpriteCollectionData collection, Material[] replacements,
            IList<FrameSnapshot> snapshots, Material[] originals)
        {
            if (!collection || replacements == null)
                return 0;
            int bound = 0;
            foreach (var snapshot in snapshots)
            {
                var definition = snapshot.Definition;
                int index = definition.materialId;
                if (index < 0 || index >= replacements.Length || !replacements[index])
                {
                    index = FindMaterialIndex(snapshot.MaterialInst, snapshot.Material, originals);
                    if (index < 0 || index >= replacements.Length || !replacements[index])
                        continue;
                }
                definition.material = replacements[index];
                definition.materialInst = replacements[index];
                bound++;
            }
            collection.materials = replacements;
            collection.materialInsts = replacements;
            if (replacements.Length > 0)
                collection.material = replacements[0];
            return bound;
        }

        internal static void Restore(IList<FrameSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                snapshot.Definition.material = snapshot.Material;
                snapshot.Definition.materialInst = snapshot.MaterialInst;
            }
        }

        private static int FindMaterialIndex(Material runtime, Material asset, Material[] originals)
        {
            if (originals == null)
                return -1;
            for (int i = 0; i < originals.Length; i++)
                if (originals[i] == runtime || originals[i] == asset)
                    return i;
            return -1;
        }
    }
}
