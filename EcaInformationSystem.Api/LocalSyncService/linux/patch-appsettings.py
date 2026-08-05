#!/usr/bin/env python3
"""Bakes the settings the LocalSync service needs directly into
appsettings.json in this same folder. Idempotent — safe to run every time
install-service.sh runs (e.g. after republishing). Mirrors
windows/patch-appsettings.ps1 exactly."""
import json
import os
import sys

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "appsettings.json")

with open(path, "r") as f:
    data = json.load(f)

data["SyncOnlyMode"] = True
data.setdefault("ZkDirect", {})
data["ZkDirect"]["Enabled"] = True
data["ZkDirect"]["RegionCode"] = 1600000000
data["ZkDirect"]["CommKey"] = 0
data["ZkDirect"]["PollIntervalSeconds"] = 60
data["ZkDirect"]["TimeoutMs"] = 20000

with open(path, "w") as f:
    json.dump(data, f, indent=2)

print("appsettings.json updated (SyncOnlyMode + ZkDirect enabled).")
