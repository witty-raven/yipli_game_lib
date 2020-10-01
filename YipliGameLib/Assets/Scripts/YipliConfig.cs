﻿using UnityEngine;


[CreateAssetMenu]
public class YipliConfig : ScriptableObject
{
    [HideInInspector]
    public string callbackLevel;

    [HideInInspector]
    public YipliPlayerInfo playerInfo;

    [HideInInspector]
    public YipliMatInfo matInfo;

    [HideInInspector]
    public string userId;

    public bool matPlayMode;

    [HideInInspector]
    public bool bIsMatIntroDone;

    [HideInInspector]
    public MP_GameStateManager MP_GameStateManager;
}
