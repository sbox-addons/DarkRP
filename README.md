# DarkRP for s&box

> A roleplay addon for **s&box Sandbox**.

**Steam Workshop:** [DarkRP for
s&box](https://steamcommunity.com/workshop/filedetails/?id=3846229017)\
**Original project:** [FPtje/DarkRP on
GitHub](https://github.com/FPtje/DarkRP)

------------------------------------------------------------------------

## About

DarkRP for s&box is a port of [DarkRP](https://github.com/FPtje/DarkRP),
the roleplay gamemode for Garry's Mod, rewritten for s&box using **C#
scripts instead of Lua**.

## Credits and licensing

DarkRP is copyright © 2014 Falco Peijnenburg and is licensed under the
MIT License.

### MIT License

``` text
The MIT License (MIT)

Copyright (c) 2014 Falco Peijnenburg

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## What was ported

The addon is derived from DarkRP and rewritten for s&box. The port
includes:

### Core gameplay and modules

-   Jobs, money and salaries
-   Police systems: wanted status, warrants and arrests
-   Mayor systems: laws, lockdowns and lotteries
-   Doors, voting, hunger and hit contracts
-   Chat commands
-   Shipments and buyable entities

Relevant source areas: `scripts/darkrp/core` and
`scripts/darkrp/modules`.

### Defaults and configuration

The port includes the default jobs, shipments, entities, ammunition,
food, categories and settings.

Relevant files:

-   `scripts/darkrp/core/RPDefaults.csx`
-   `scripts/darkrp/core/RPConfig.csx`

These defaults use s&box models and clothing instead of Half-Life 2 /
Counter-Strike content.

### Weapons and equipment

The addon includes:

-   Arrest / unarrest baton
-   Stunstick
-   Battering ram
-   Keys and lockpick
-   Pocket
-   Medic kit
-   Weapon checker
-   Keypad checker
-   DarkRP guns

Relevant source areas: `scripts/darkrp/weapons` and `darkrp/weapons`.

### UI, protection and compatibility

-   Weapon icons in `ui/darkrp/weapons`, converted from DarkRP's
    `content/materials/vgui/entities`
-   `disabled_defaults`, based on `darkrpmodification`'s
    `disabled_defaults.lua`
-   Prop protection following the ideas of Falco's Prop Protection
    (FPP), which ships with DarkRP

The prop-protection implementation is in
`scripts/darkrp/modules/RPPropProtection.csx`.

## Assets and model credits

The guns, along with the arm animations for the batons and door ram,
come from **Sandbox** and are licensed under the MIT License by
Facepunch Studios.

The following models in `models/darkrp` were created specifically for
this addon:

-   Stun baton
-   Door ram
-   Law board
-   Money models
