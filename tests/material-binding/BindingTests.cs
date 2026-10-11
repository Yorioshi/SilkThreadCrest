using System;
using SilkThreadCrest;
using UnityEngine;

namespace UnityEngine {
public class Object {
 public static implicit operator bool(Object value) => value != null;
}
public class Material : Object { public string Name; public Material(string name) { Name=name; } }
}
public class tk2dSpriteDefinition { public Material material, materialInst; public int materialId; }
public class tk2dSpriteCollectionData : UnityEngine.Object {
 public tk2dSpriteDefinition[] spriteDefinitions;
 public Material[] materials, materialInsts; public Material material;
 public tk2dSpriteCollectionData inst => this;
}
public class tk2dBaseSprite : UnityEngine.Object {
 protected tk2dSpriteCollectionData collectionInst;
 public tk2dSpriteCollectionData Collection { get; set; }
 public void SetRuntime(tk2dSpriteCollectionData c) { collectionInst=c; }
}
class BindingTests {
 static int tests;
 static void Assert(bool pass,string name) { if(!pass) throw new Exception(name); Console.WriteLine("PASS "+name); tests++; }
 static void Main() {
 var a=new Material("asset"); var r=new Material("runtime"); var n=new Material("recolored");
 var frame=new tk2dSpriteDefinition {material=a,materialInst=r,materialId=0};
 var c=new tk2dSpriteCollectionData {spriteDefinitions=new[]{frame},materials=new[]{a},materialInsts=new[]{r},material=a};
 var snapshots=SilkThreadMaterialBinding.Capture(c);
 Assert(SilkThreadMaterialBinding.Bind(c,new[]{n},snapshots,new[]{r})==1,"one frame bound");
 Assert(frame.materialInst==n && frame.material==n,"UpdateMaterial reads the NEW frame materialInst");
 Assert(c.materialInsts[0]==n,"collection runtime array also updated");
 SilkThreadMaterialBinding.Restore(snapshots);
 Assert(frame.material==a && frame.materialInst==r,"restore exact asset and runtime frame references");
 var originalCollection=new tk2dSpriteCollectionData();
 var sprite=new tk2dBaseSprite {Collection=originalCollection}; sprite.SetRuntime(c);
 Assert(SilkThreadMaterialBinding.RuntimeCollection(sprite)==c,"platform runtime collection preferred over source collection");
 var invalid=new tk2dSpriteDefinition {material=a,materialInst=r,materialId=100};
 c.spriteDefinitions=new[]{invalid}; snapshots=SilkThreadMaterialBinding.Capture(c);
 Assert(SilkThreadMaterialBinding.Bind(c,new[]{n},snapshots,new[]{r})==1 && invalid.materialInst==n,"invalid materialId safely resolved by original material reference");
 invalid.materialId=100; invalid.materialInst=new Material("unrelated"); invalid.material=invalid.materialInst;
 snapshots=SilkThreadMaterialBinding.Capture(c);
 Assert(SilkThreadMaterialBinding.Bind(c,new[]{n},snapshots,new[]{r})==0,"unmatched frame not rebound to wrong atlas");
 bool embedded;
 var bytes=SilkThreadSkinAssetReader.Read("/nonexistent/Silk Thread Crest/Skins/skin.png", typeof(BindingTests).Assembly, "test.skin.png", out embedded);
 Assert(embedded && bytes.Length>1000 && bytes[0]==137 && bytes[1]==80,"missing external PNG loads actual embedded PNG despite spaces in path");
 string temp=System.IO.Path.GetTempFileName();
 try {
   System.IO.File.WriteAllBytes(temp,new byte[]{1,2,3});
   bytes=SilkThreadSkinAssetReader.Read(temp,typeof(BindingTests).Assembly,"test.skin.png",out embedded);
   Assert(!embedded && bytes.Length==3 && bytes[0]==1,"existing external image takes priority");
 } finally {System.IO.File.Delete(temp);}
 bool missing=false;
 try { SilkThreadSkinAssetReader.Read("/nonexistent.png",typeof(BindingTests).Assembly,"no.resource",out embedded); }
 catch(System.IO.FileNotFoundException) {missing=true;}
 Assert(missing,"missing embedded resource reports explicit failure");
 Console.WriteLine(tests+" tests passed; stubs validate references, not Unity rendering.");
 }
}
