![description](https://i.postimg.cc/Nj1kk5Bp/AUGL.gif)

[![GitHub Repo stars](https://img.shields.io/github/stars/auratech0/au-glitched-lobbies?style=flat&logo=github&color=yellow)](https://github.com/auratech0/au-glitched-lobbies/stargazers)
[![Discord](https://img.shields.io/discord/1403448096066633748?style=flat&logo=discord&logoColor=white&label=Join%20Discord&color=5865F2)](https://discord.gg/HeNGYArCkY)
![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=c-sharp&logoColor=white)

# augl-doc-n-menu
Documentation for AUGL (Among Us Glitched Lobbies) along with its own mod menu and custom things

Coded along with [Sparxist](https://github.com/Sparxist)

A big thanks to all of our contributors who put their time into this menu ❤️

Many **open-source** components from [g0aty's SickoMenu](https://github.com/g0aty/SickoMenu)

Our Discord Servers:

Tech Lounge: [Discord](https://discord.gg/rj3fWwrc8Q)

AU Glitched Lobbies: [Discord](https://dsc.gg/AUGL)

Join both of our Discords for test versions and to support us so we can continue producing more versions of this mod and continue this long lived project of AUGL.

<details>
<summary><b> CRITICAL DISCLAIMER & HARMONY HOOK WARNING (Click to expand)</b></summary>

### HIGH-RISK EXPERIMENTAL ENVIRONMENT & BAN NOTICE
This repository contains advanced BepInEx plugin features, utilizing the Harmony hooking framework to alter runtime assembly logic and network event triggers.

* **IMMEDIATE SERVER DETECTION:** Altering core game methods via runtime hooks will immediately trigger modern server-side validation checks (e.g., native Innersloth anti-cheat). This can result in permanent IP/HWID bans.
* **NO LIABILITY:** THIS PIECE OF OPEN-SOURCE, WHICH IS LICENSED AS AGPL 3.0 IS PROVIDED IN AN "AS IS" BASIS, WITHOUT WARRANTIES OF ANY KIND. The authors assume zero responsibility for account status or game instability.
* **NON-AFFILIATION:** This project is 100% unofficial and is NOT associated, affiliated, or officially connected with Innersloth LLC.

*For the full legal and technical text, see the official [DISCLAIMERS.md](https://github.com/auratech0/au-glitched-lobbies/blob/main/docs/DISCLAIMERS.md) file.*
</details>

---


# What is AUGL Mod/AUGL Menu?
This is a multi-purpose but small menu meant to enhance gameplay. It's made for finding and creating Glitched Lobbies, packed with a custom regionInfo.json file with 7 modded regions (Modded EU, Modded AS, Modded NA, NikoEU, NikoAS, NikoNA and AllOfUs EU) and AUGL's own "region" which lets you create glitched lobbies **only** on official regions and **only** if they are available.



# What other things can this menu offer?
Many things, like 2 custom gamemodes (Shift And Seek and 0cd Shields), platform spoofing to PlayStation, typeable settings, modded client detection and partial support and up to ~28% better performance*

<sup><sub>*This was tested on a 2011 Mac Mini with EndeavourOS and the Steam version of the game. Intel Core i5-2415M, Intel Graphics HD3000, 8GB DDR3 RAM and 240GB SSD. Performance boosts may be not present at all, may be less, may be more or may even make the game lag slightly. Depends on the computer.</sub></sup>



# What's a glitched lobby?
Glitched Among Us servers forget your level. In a glitch lobby, the server shows level 1, but your true level is higher, for example 100. This mismatch changes how the server counts your level gain. When the server tries to raise your level from 1 to 2, it uses your true level instead. The server raises your level from 100 to 101. This is how players raise their level past 100.

Each lobby has a code. The last four letters of this code identify the server. Check these four letters against a list of glitch codes on [this](https://augl.net) website. This check tells you if your server is glitched. For example, assume code XYZW is a glitch code. Lobby ABXYZW ends in XYZW, hence this lobby would be glitched.
