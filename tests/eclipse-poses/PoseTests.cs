using System;
using SilkThreadCrest;
class PoseTests {
 static int count;
 static void Check(string name,int frame,int length,int expected) {
 int actual=SilkThreadEclipsePose.Select(name,frame,length);
 if(actual!=expected) throw new Exception(name+" frame="+frame+": "+actual+" != "+expected);
 count++;
 }
 static void Main() {
 Check("Idle",0,10,0); Check("Idle",6,10,1);
 Check("Run",0,12,2); Check("Run",8,12,3);
 Check("Walk",-10,8,2); Check("Walk",99,8,3);
 Check("Idle",0,0,0); Check("Slash",0,10,-1);
 Check("Slash",4,10,6); Check("Slash",9,10,-1);
 Check("SlashAlt",3,10,6); Check("Slash_Charged",4,10,-1);
 Check("Bind",3,10,7); Check("Focus",3,10,7);
 Check("Fall",3,10,5); Check("Jump",3,10,4);
 Check("Double Jump",3,10,-1); Check("Wall Slide",3,10,-1);
 Check("DownSpikeBounce 1",3,10,-1); Check("UpSlash",3,10,-1);
 Check("Idle Hurt",0,10,-1); Check("Dash To Idle",0,10,-1);
 Check("ToolThrow Q",0,10,-1); Check("Wake Up Ground",0,10,-1);
 Check("Harpoon Catch",0,10,-1); Check("Super Jump Fall",0,10,-1);
 Check("Unknown Special",0,10,-1); Check(null,0,0,-1);
 Check("Shuttlecock",0,10,-1); Check("Fireball",0,10,-1);
 Console.WriteLine(count+" pose-selection tests passed. No Unity rendering tested.");
 }
}
