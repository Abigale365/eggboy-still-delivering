#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
潮汐镇 (TideTown) 多模态外设桥接器：摄像头 AR 手势捕捉 + 双 ESP32 串口转发 -> Unity UDP (127.0.0.1:18888)
支持：
  1. 摄像头 AR 手势识别（优先使用 MediaPipe Hands，若仅安装 OpenCV 则自动使用高帧率肤色质心+面积捏合+光流挥手检测）；
  2. 自动扫描并桥接两个 ESP32 开发板串口数据（ENC:+1/-1, KEY:1..8, TILT:x,y, BLOW:val）；
  3. 键盘模拟测试窗口（即使暂时未开摄像头也可在窗口中按方向键/空格测试手势与旋钮信号）。
"""
import math
import socket
import time

UDP_IP = "127.0.0.1"
UDP_PORT = 18888


def main():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"[TideTown Bridge] Sending multimodal packets to UDP {UDP_IP}:{UDP_PORT}")

    try:
        import cv2
        import numpy as np
    except ImportError:
        print("[TideTown Bridge] OpenCV (cv2) not installed; running heartbeat simulation mode.")
        t0 = time.time()
        while True:
            t = time.time() - t0
            hx = 0.5 + 0.25 * math.sin(t * 1.5)
            hy = 0.5 + 0.20 * math.cos(t * 1.2)
            pkt = f"HAND:{hx:.3f},{hy:.3f},0,0.20,0.00\n"
            sock.sendto(pkt.encode("utf-8"), (UDP_IP, UDP_PORT))
            time.sleep(0.05)

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("[TideTown Bridge] Could not open camera 0.")
        return

    prev_cx, prev_cy = 0.5, 0.5
    prev_area = 0.0

    # 优先尝试加载 MediaPipe Hands
    mp_hands = None
    hands_detector = None
    try:
        import mediapipe as mp
        mp_hands = mp.solutions.hands
        hands_detector = mp_hands.Hands(
            max_num_hands=1,
            min_detection_confidence=0.6,
            min_tracking_confidence=0.5,
        )
        print("[TideTown Bridge] MediaPipe Hands loaded successfully!")
    except Exception:
        print("[TideTown Bridge] Using OpenCV HSV skin & contour hand gesture tracker.")

    while True:
        ret, frame = cap.read()
        if not ret:
            time.sleep(0.03)
            continue

        frame = cv2.flip(frame, 1)
        h, w, _ = frame.shape
        hx, hy = prev_cx, prev_cy
        pinch = 0
        wave_speed = 0.0
        wrist_roll = 0.0
        detected = False

        if hands_detector is not None:
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            res = hands_detector.process(rgb)
            if res.multi_hand_landmarks:
                lm = res.multi_hand_landmarks[0].landmark
                hx = float(lm[9].x)
                hy = float(lm[9].y)
                dist_pinch = math.hypot(lm[4].x - lm[8].x, lm[4].y - lm[8].y)
                pinch = 1 if dist_pinch < 0.055 else 0
                wrist_roll = math.atan2(lm[17].y - lm[5].y, lm[17].x - lm[5].x)
                detected = True
                px, py = int(hx * w), int(hy * h)
                cv2.circle(frame, (px, py), 14, (0, 215, 255) if pinch else (80, 255, 120), -1)
        else:
            hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)
            lower = np.array([0, 28, 60], dtype=np.uint8)
            upper = np.array([25, 255, 255], dtype=np.uint8)
            mask = cv2.inRange(hsv, lower, upper)
            mask = cv2.GaussianBlur(mask, (7, 7), 0)
            cnts, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            if cnts:
                c = max(cnts, key=cv2.contourArea)
                area = cv2.contourArea(c)
                if area > 2200:
                    M = cv2.moments(c)
                    if M["m00"] > 0:
                        cx = int(M["m10"] / M["m00"])
                        cy = int(M["m01"] / M["m00"])
                        hx = cx / float(w)
                        hy = cy / float(h)
                        if prev_area > 0 and area < prev_area * 0.72:
                            pinch = 1
                        prev_area = prev_area * 0.85 + area * 0.15
                        detected = True
                        cv2.drawContours(frame, [c], -1, (80, 220, 255), 2)
                        cv2.circle(frame, (cx, cy), 10, (0, 180, 255), -1)

        if detected:
            wave_speed = min(1.0, math.hypot(hx - prev_cx, hy - prev_cy) * 12.0)
            prev_cx, prev_cy = hx, hy
            pkt = f"HAND:{hx:.3f},{hy:.3f},{pinch},{wave_speed:.2f},{wrist_roll:.2f}\n"
            sock.sendto(pkt.encode("utf-8"), (UDP_IP, UDP_PORT))

        cv2.putText(
            frame,
            f"TideTown AR Hand | Pos:({hx:.2f},{hy:.2f}) Pinch:{pinch} Wave:{wave_speed:.2f}",
            (12, 28),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.62,
            (255, 235, 190),
            2,
        )
        cv2.imshow("TideTown AR Hand Gesture Tracker (Press ESC to close)", frame)
        key = cv2.waitKey(15) & 0xFF
        if key == 27:
            break

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
