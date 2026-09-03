# Legal Disclaimers & Technical Notices

This document outlines the operational boundaries, non-affiliation status, and liability limitations for the **au-glitched-lobbies** project.

---

## 1. Technical Operation & Code Integrity
* **No Code Injection:** This software functions strictly as a network region configurator. It does not read, write, hook, or inject code into the active memory of the *Among Us* executable client (`.exe`, `.apk`, etc.).
* **No File Alteration:** This tool does not modify, patch, or alter any core game assets or binary files distributed by Innersloth.
* **Network Configuration Only:** It operates solely by replacing or modifying the local `regionInfo.json` configuration file. This safely routes lobby search queries to alternative, community-hosted master servers.

## 2. Official Non-Affiliation Clause
* **100% Unofficial:** This project is entirely community-made and open-source. It is **NOT** associated, affiliated, authorized, endorsed by, or in any way officially connected with Innersloth LLC, or any of its subsidiaries or affiliates.
* **Voluntary Engagement:** Innersloth does not endorse, enforce, or mandate the use of this custom configuration tool. The official *Among Us* game client functions fully without this software. 
* **Discretionary Use:** Choosing to install and utilize this custom network routing layout is a purely voluntary action taken at the user's sole discretion and risk.
* **Trademark Acknowledgment:** The name *Among Us*, along with all associated logos, characters, marks, and designs, are the exclusive registered trademarks of Innersloth LLC.

## 3. Account Progression, Data Syncing & Liability
* **Server-Side Anomaly:** Bypassing the standard Level 100 visual cap relies entirely on specific room-code routing filters and server-side synchronization behaviors within alternative networks. 
* **Apache 2.0 Compliance:** As defined by the **Apache License 2.0**, this software is provided on an **"AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND**, either express or implied.
* **No Liability:** The authors, maintainers, and contributors of this repository hold zero liability for individual player account statuses, database level rollbacks, statistics resets, or structural account restrictions enacted by Innersloth's server updates.

## 4. Fair Use & Educational Intent
This utility is published under the spirit of open-source research regarding multiplayer network routing protocols and local configuration environments. It is intended solely for private, educational, and custom match settings with consenting participants.

## 5. STRICT RUNTIME PATCHING & EXPERIMENTAL FEATURE WARNING
> [!WARNING]
> ### ⚠️ HIGH-RISK HARMONY HOOK ENVIRONMENT & IMMEDIATE BAN NOTICE
> This repository contains advanced BepInEx plugin features utilizing the Harmony hooking framework to alter runtime assembly logic, game state parameters, and network event triggers (commonly used for experimental lobby behavior or "trolling" features).
> 
> By compiling or installing this BepInEx plugin, you are explicitly warned of the following:
> 
> * **IMMEDIATE SERVER DETECTION & PERMANENT BANS:** Altering core game methods via runtime hooks (such as modifying player interaction states, cooldowns, or visibility logic) will immediately trigger modern server-side validation checks (e.g., HostGuard or native Innersloth anti-cheat). This can result in permanent IP bans, Hardware ID (HWID) bans, and account termination.
> * **UNMANAGED RUNTIME INSTABILITY:** Runtime code patching can cause severe client crashes, broken lobby states, and corruption of local profile cache data. The developers assume zero responsibility for broken game installations.
> * **NO GRIEFING POLICY:** The deployment of these runtime modifications to maliciously disrupt public matchmaking or grief unconsenting players is strictly prohibited. This framework is designed exclusively for educational research and private sandbox environments.
> 
> **ANY USE OUTSIDE OF PRIVATE TESTING IS DONE AT YOUR OWN PERIL. YOU HAVE BEEN WARNED.**


This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.
