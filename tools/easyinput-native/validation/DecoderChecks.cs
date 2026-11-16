using System;
using System.Text;
class DecoderChecks
{
 static int assertions;
 static void Expect(string actual,string expected)
 {
  assertions++;
  if(actual!=expected) throw new Exception("Expected "+expected+" received "+actual);
 }
 static byte[] Frame(byte type,byte[] payload)
 {
  byte[] data=new byte[5+payload.Length];
  data[0]=17;data[1]=type;data[3]=1;data[4]=(byte)payload.Length;
  Buffer.BlockCopy(payload,0,data,5,payload.Length);return data;
 }
 static string Action(EasyInputReportDecoder d,char suffix)
 {
  var data=Frame(5,Encoding.ASCII.GetBytes("c0ffee00-0000-4000-8000-00000000000"+suffix));
  return d.Decode(data,data.Length);
 }
 static string Fn(EasyInputReportDecoder d,byte state)
 {
  var data=Frame(2,new byte[]{state,70,110});return d.Decode(data,data.Length);
 }
 static void Main()
 {
  var d=new EasyInputReportDecoder();
  string[] expected={"s1","s2","s3","s4","s5","s6","s7","s8","dial-left","dial-right","dial-press"};
  for(int i=0;i<11;i++) Expect(Action(d,"123456789ab"[i]),expected[i]);
  for(int i=0;i<8;i++) {Expect(Fn(d,1),null);Expect(Action(d,"12345678"[i]),"combo-s"+(i+1));Expect(Fn(d,2),null);}
  Expect(Fn(d,1),null);Expect(Fn(d,2),"dial-press");
  Fn(d,1);Expect(Action(d,'9'),"dial-left");Expect(Fn(d,2),null);
  Fn(d,1);d.Reset();Expect(Fn(d,2),null);Expect(Action(d,'1'),"s1");
  Expect(d.Decode(null,0),null);
  for(int i=0;i<41;i++) {var data=Frame(5,Encoding.ASCII.GetBytes("c0ffee00-0000-4000-8000-000000000001"));Expect(d.Decode(data,i),null);}
  Expect(Action(d,'z'),null);
  Console.WriteLine("PASS "+assertions+" C# decoder assertions");
 }
}
