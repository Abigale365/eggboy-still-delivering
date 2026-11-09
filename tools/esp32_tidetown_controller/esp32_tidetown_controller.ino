/*
 * 潮汐镇 (TideTown) 双 ESP32 硬件外设固件（支持 8键+旋钮开发板 & 全套传感器开发板）
 * 波特率：115200（Unity 内置的 HardwareAndSensorHub 会自动扫描 COM3..COM24 即插即用！）
 *
 * 串口协议（每行一条指令）：
 *   ENC:+1 / ENC:-1      -> 旋转编码器顺时针/逆时针拨动 3D 鲁班齿轮环、旋转 3D 旧物、摇起渡口浮桥、转动灯塔光束
 *   KEY:1 .. KEY:8       -> 8 个实体按键：
 *                           KEY 1~3：拨动甲栓/乙栓/丙栓（或按 3D 铜扣 ①②③ / 弹奏铜铃音阶）
 *                           KEY 4~6：切换旋钮控制的齿轮环（外环 / 中环 / 内环）
 *                           KEY 7（或旋钮下按）：推动中心主轴开锁 / 场景互动 (E)
 *                           KEY 8：呼出或收起【潮汐镇 AI 灵枢与实时造物台】(V)
 *   TILT:x,y             -> 传感器板倾角/摇杆输入（用于海风浪涌平衡挑战与 3D 视角倾斜）
 *   BLOW:1.0             -> 声音/吹气传感器触发（助燃面包房烤炉、冲开第一重三连木栓）
 */

// --- 旋钮编码器引脚配置（可根据你的开发板丝印微调） ---
const int PIN_ENC_CLK = 18;
const int PIN_ENC_DT  = 19;
const int PIN_ENC_SW  = 21; // 旋钮按下相当于 KEY:7

// --- 8 个独立按键引脚配置 ---
const int KEY_PINS[8] = { 13, 14, 27, 26, 25, 33, 32, 4 };

// --- 可选：第二块全套开发板的麦克风/吹气模拟口（GPIO 34） ---
const int PIN_MIC_ADC = 34;

int lastClkState = HIGH;
bool lastKeyStates[8];
bool lastSwState = HIGH;
unsigned long lastDebounceTime = 0;

void setup() {
  Serial.begin(115200);

  pinMode(PIN_ENC_CLK, INPUT_PULLUP);
  pinMode(PIN_ENC_DT, INPUT_PULLUP);
  pinMode(PIN_ENC_SW, INPUT_PULLUP);

  for (int i = 0; i < 8; i++) {
    pinMode(KEY_PINS[i], INPUT_PULLUP);
    lastKeyStates[i] = HIGH;
  }

  lastClkState = digitalRead(PIN_ENC_CLK);
  Serial.println("SYS:ESP32_TIDETOWN_READY");
}

void loop() {
  // 1. 读取旋转编码器
  int clkState = digitalRead(PIN_ENC_CLK);
  if (clkState != lastClkState && clkState == LOW) {
    int dtState = digitalRead(PIN_ENC_DT);
    if (dtState != clkState) {
      Serial.println("ENC:+1");
    } else {
      Serial.println("ENC:-1");
    }
  }
  lastClkState = clkState;

  // 2. 读取旋钮下按与 8 个按键
  if (millis() - lastDebounceTime > 25) {
    bool swState = digitalRead(PIN_ENC_SW);
    if (swState == LOW && lastSwState == HIGH) {
      Serial.println("KEY:7");
      lastDebounceTime = millis();
    }
    lastSwState = swState;

    for (int i = 0; i < 8; i++) {
      bool st = digitalRead(KEY_PINS[i]);
      if (st == LOW && lastKeyStates[i] == HIGH) {
        Serial.print("KEY:");
        Serial.println(i + 1);
        lastDebounceTime = millis();
      }
      lastKeyStates[i] = st;
    }
  }

  delay(2);
}
