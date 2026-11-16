using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Threading;
namespace UnityEngine {
 public enum KeyCode {W,A,S,D,E,Space,M,Escape,V,Tab,R,B,F}
 public enum RuntimeInitializeLoadType {BeforeSceneLoad}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int n){}}
 public class GameObject {public GameObject(string name){} public T AddComponent<T>() where T:new(){return new T();}}
 public class MonoBehaviour {public GameObject gameObject=new GameObject("");public static void DontDestroyOnLoad(object o){}}
 public static class Time {public static int frameCount;public static float unscaledTime;}
 public static class Input {public static bool GetKeyDown(KeyCode k){return false;}public static bool GetKey(KeyCode k){return false;}}
 public static class Application {public static bool isFocused=true;public static string streamingAssetsPath=""; public static string persistentDataPath="";}
 public static class Debug {public static void LogWarning(string m){}}
}
namespace UnityEngine.Scripting {public class PreserveAttribute:Attribute{}}
public static class WoodenMechanismPuzzleModal {
 public static bool IsOpen;public static int ActiveRingSelection;
 public static void ExternalToggleLatch(int i){}public static void ExternalRotateRing(int i,int d){}public static void ExternalAttemptUnlock(){}
}
class NativeChecks {
 static void Set(object o,string field,object value){o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);}
 static void Call(object o,string method,params object[] args){o.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static byte[] Frame(byte type,byte[] payload){var data=new byte[5+payload.Length];data[0]=17;data[1]=type;data[3]=1;data[4]=(byte)payload.Length;Buffer.BlockCopy(payload,0,data,5,payload.Length);return data;}
 static void Main(){
  var bridge=new EasyInputNativeBridge();var actions=new EasyInputBridge();
  var receiver=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));receiver.Client.Blocking=false;
  var sender=new UdpClient();var token=Guid.NewGuid().ToByteArray();var endpoint=(IPEndPoint)receiver.Client.LocalEndPoint;
  Set(bridge,"receiver",receiver);Set(bridge,"token",(byte[])token.Clone());Set(bridge,"actions",actions);Set(bridge,"helper",Process.GetCurrentProcess());Set(bridge,"focused",true);
  Action<byte,byte[]> send=(kind,data)=>{var packet=new byte[17+data.Length];Buffer.BlockCopy(token,0,packet,0,16);packet[16]=kind;Buffer.BlockCopy(data,0,packet,17,data.Length);sender.Send(packet,packet.Length,endpoint);Thread.Sleep(10);Call(bridge,"Update");};
  var s1=Frame(5,Encoding.ASCII.GetBytes("c0ffee00-0000-4000-8000-000000000001"));
  send(1,new byte[0]);send(2,s1);UnityEngine.Time.frameCount++;
  Check(EasyInputBridge.Down(UnityEngine.KeyCode.W),"raw report did not reach input");Check(EasyInputBridge.Held(UnityEngine.KeyCode.W),"movement pulse missing");
  Call(bridge,"OnApplicationFocus",false);Check(!EasyInputBridge.Held(UnityEngine.KeyCode.W),"focus did not clear hold");
  send(1,new byte[0]);send(2,s1);UnityEngine.Time.frameCount++;Check(!EasyInputBridge.Down(UnityEngine.KeyCode.W),"unfocused input accepted");
  Call(bridge,"OnApplicationFocus",true);send(1,new byte[0]);
  send(2,Frame(2,new byte[]{1,70,110}));Call(bridge,"OnApplicationFocus",false);Call(bridge,"OnApplicationFocus",true);send(1,new byte[0]);send(2,Frame(2,new byte[]{2,70,110}));UnityEngine.Time.frameCount++;Check(!EasyInputBridge.Down(UnityEngine.KeyCode.E),"stale Fn release became tap");
  send(2,Frame(5,Encoding.ASCII.GetBytes("c0ffee00-0000-4000-8000-000000000005")));Check(EasyInputBridge.Held(UnityEngine.KeyCode.E),"interaction missing");send(0,new byte[0]);Check(!EasyInputBridge.Held(UnityEngine.KeyCode.E),"disconnect did not clear hold");
  send(1,new byte[0]);send(2,s1);UnityEngine.Time.unscaledTime=2;Call(bridge,"Update");Check(!EasyInputBridge.Held(UnityEngine.KeyCode.W),"heartbeat timeout did not clear");
  token[0]^=1;send(1,new byte[0]);send(2,s1);UnityEngine.Time.frameCount++;Check(!EasyInputBridge.Down(UnityEngine.KeyCode.W),"invalid token accepted");
  receiver.Close();sender.Close();Console.WriteLine("PASS native UDP routing, focus suppression, Fn reset, disconnect, heartbeat expiry, session authentication (Unity API stubs)");
 }
}
