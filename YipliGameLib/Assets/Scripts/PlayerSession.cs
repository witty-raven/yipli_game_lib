﻿using Firebase.Database;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YipliFMDriverCommunication;

public class PlayerSession : MonoBehaviour
{
    private string userId = ""; // to be recieved from Yipli
    public string gameId = ""; // to be assigned to every game.
    private string playerId = ""; // to be recieved from Yipli for each game
    private float points; // Game points / coins
    private string playerAge = ""; //Current age of the player
    private string playerHeight = ""; //Current height of the player
    private string playerWeight = ""; //Current height of the player
    private string matId = "";
    private string matMacAddress;
    private float duration;
    private float calories;
    private float fitnesssPoints;
    private int xp;
    public string intensityLevel = ""; // to be decided by the game.
    private IDictionary<YipliUtils.PlayerActions, int> playerActionCounts; // to be updated by the player movements
    private IDictionary<string, string> playerGameData; // to be used to store the player gameData like Highscore, last played level etc.


    public TextMeshProUGUI bleErrorText;

    public GameObject BleErrorPanel;
    public GameObject LoadingScreen;
    private GameObject instantiatedBleErrorPanel;
    private bool bIsBleConnectionCoroutineRunning = false;

    [JsonIgnore]
    public YipliConfig currentYipliConfig;

    [JsonIgnore]
    private bool bIsPaused; // to be set, when game is paused.

    [JsonIgnore]
    private static PlayerSession _instance;

    [JsonIgnore]
    public static PlayerSession Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.Log("Destroying current instance of playersession and reinitializing");
            Destroy(gameObject);
            _instance = this;
        }
        else
        {
            _instance = this;
        }

        if (currentYipliConfig.playerInfo == null)
        {
            // Call Yipli_GameLib_Scene
            _instance.currentYipliConfig.callbackLevel = SceneManager.GetActiveScene().name;
            Debug.Log("Updating the callBackLevel Value to :" + _instance.currentYipliConfig.callbackLevel);
            Debug.Log("Loading Yipli scene for player Selection...");
            if (!_instance.currentYipliConfig.callbackLevel.Equals("Yipli_Testing_harness"))
                SceneManager.LoadScene("yipli_lib_scene");
        }
        else
        {
            Debug.Log("Current player is not null.");
        }
    }

    public void Start()
    {
        Debug.Log("Starting the BLE routine check in PlayerSession Start()");
        if (!_instance.currentYipliConfig.callbackLevel.Equals("Yipli_Testing_harness"))
            StartCoroutine(CheckBleRoutine());
    }

    public void Update()
    {
        try
        {
            Debug.Log("Game Cluster Id : " + YipliHelper.GetGameClusterId());
        }
        catch(Exception exp)
        {
            Debug.Log("Exception is getting Cluster ID" + exp.Message);
        }
    }
    public string GetCurrentPlayer()
    {
        return currentYipliConfig.playerInfo.playerName;
    }

    public string GetCurrentPlayerId()
    {
        return currentYipliConfig.playerInfo.playerId;
    }


    public void ChangePlayer()
    {
        _instance.currentYipliConfig.callbackLevel = SceneManager.GetActiveScene().name;
        Debug.Log("Updating the callBackLevel Value to :" + _instance.currentYipliConfig.callbackLevel);
        Debug.Log("Loading Yipli scene for player Selection...");
        SceneManager.LoadScene("yipli_lib_scene");
    }

    //Pass here name of the game
    public void UpdateGameData(Dictionary<string, string> update)
    {
        if (update != null)
        {
            playerGameData = new Dictionary<string, string>();
            playerGameData = update;
        }
    }

    // Get player game data
    public async Task<DataSnapshot> GetGameData(string gameId)
    {
        DataSnapshot dataSnapShot = await FirebaseDBHandler.GetGameData(
            currentYipliConfig.userId,
            currentYipliConfig.playerInfo.playerId,
            gameId,
            () => { Debug.Log("Got Game data successfully"); }
        );
        return dataSnapShot ?? null;
    }

    //First function to be called only once when the game starts()
    //To be used for error handling
    //Call in case of exception while playing game.
    public void CloseSPSession()
    {
        //Destroy current player session data
        calories = 0;
        fitnesssPoints = 0;
        points = 0;
        duration = 0;
        Debug.Log("Aborting current player session.");
    }



    //Function to validate all the session parameters before writing to DB
    //To be called from GamePause function
    //To be called from GameResume function


    //to be called from all the player movment actions handled script
    //To be called from GameObject FixedUpdate
    public void UpdateDuration()
    {
        Debug.Log("Updating duration for current player session.");
        if (bIsPaused == false)
        {
            duration += Time.deltaTime;
        }

        if (YipliHelper.GetBleConnectionStatus().Equals("Connected", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("In UpdateDuration : Ble connected");
            if (BleErrorPanel.activeSelf)
            {
                FindObjectOfType<YipliAudioManager>().Play("BLE_success");
                BleErrorPanel.SetActive(false);
            }
        }
    }

    private IEnumerator CheckBleRoutine()
    {
        int autoRetryBleConnectionCount = 10;
        while (true)
        {
            yield return new WaitForSecondsRealtime(.15f);
            string retStatus = YipliHelper.GetBleConnectionStatus();
            Debug.Log("Bluetooth Status : " + retStatus);
            if (!retStatus.Equals("Connected", StringComparison.OrdinalIgnoreCase))
            {
                if (autoRetryBleConnectionCount > 0)
                {
                    Debug.Log("Mat is disconnected. Trying to connect back : " + currentYipliConfig.matInfo.macAddress);
                    try
                    {
                        InitBLE.InitBLEFramework(currentYipliConfig.matInfo.macAddress,0);
                    }
                    catch (Exception exp)
                    {
                        bleErrorText.text = "Unable to connect. Check your Mat.";
                        Debug.Log("Exception in InitBLEFramework from ReconnectBleFromGame" + exp.Message);
                    }
                    autoRetryBleConnectionCount--;
                }
                else
                {
                    //Exhausted auto reties
                    Debug.Log("Setting the Error Panel Active");
                    if (!BleErrorPanel.activeSelf)
                    {
                        bleErrorText.text = "Bluetooth Connection lost. Make sure that the Yipli Mat(default) and your device bluetooth are turned on and ReCheck.";
                        FindObjectOfType<YipliAudioManager>().Play("BLE_failure");
                        BleErrorPanel.SetActive(true);
                    }
                }
            }
            else
            {
                Debug.Log("Bluetooth connection is established.");
                Debug.Log("Destroying Ble Error canvas prefab.");
                if (BleErrorPanel.activeSelf)
                {
                    FindObjectOfType<YipliAudioManager>().Play("BLE_success");
                    BleErrorPanel.SetActive(false);
                }
                autoRetryBleConnectionCount = 10;
            }
        }
    }

    public void StartCoroutineForBleReConnection()
    {
        Debug.Log("In StartCoroutineForBleReConnection.");
        if (!bIsBleConnectionCoroutineRunning)
        {
            StartCoroutine(ReconnectBleFromGame());
        }
        else
        {
            Debug.Log("Coroutine : ReconnectBleFromGame is already running");
        }
    }

    private IEnumerator ReconnectBleFromGame()
    {
        Debug.Log("In ReconnectBleFromGame.");
        Debug.Log("Setting bIsBleConnectionCoroutineRunning to true.");
        bIsBleConnectionCoroutineRunning = true;
        bleErrorText.text = "Retrying bluetooth connection...";

        try
        {
            InitBLE.InitBLEFramework(currentYipliConfig.matInfo.macAddress, 0);
        }
        catch (Exception exp)
        {
            bleErrorText.text = "Unable to connect. Check your Mat.";
            Debug.Log("Exception in InitBLEFramework from ReconnectBleFromGame" + exp.Message);
        }

        yield return new WaitForSecondsRealtime(.35f);

        bool bIsConnected = false;
        int iTryCount = 10;
        while (iTryCount > 0)
        {
            Debug.Log("In ReconnectBleFromGame while : " + iTryCount);
            iTryCount--;
            yield return new WaitForSecondsRealtime(.25f);
            string strStatus = YipliHelper.GetBleConnectionStatus();
            if (strStatus.Equals("connected", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("Connected back");
                bleErrorText.text = "Connected back successfully";
                yield return new WaitForSecondsRealtime(.5f);

                if (BleErrorPanel.activeSelf)
                {
                    BleErrorPanel.SetActive(false);
                    FindObjectOfType<YipliAudioManager>().Play("BLE_success");
                }
                bIsConnected = true;
                iTryCount = 0;
                break;
            }
        }

        if (!bIsConnected)
        {
            //Function execution will come here only if the mat is not connected after the given no. of retries.
            bleErrorText.text = "Bluetooth Connection lost. Make sure that the Yipli Mat(default) and your device bluetooth are turned on and ReCheck.";
        }
        Debug.Log("Setting bIsBleConnectionCoroutineRunning to false");
        bIsBleConnectionCoroutineRunning = false;
    }

    public void LoadingScreenSetActive(bool bOn)
    {
        Debug.Log("Loading Screen : " + bOn);
        LoadingScreen.SetActive(bOn);
    }

    // Update store data witout gameplay. To be called by games Shop Manager.
    public async Task UpdateStoreData(string gameId, Dictionary<string, object> dStoreData)
    {
        await FirebaseDBHandler.UpdateStoreData(
            currentYipliConfig.userId,
            currentYipliConfig.playerInfo.playerId,
            gameId,
            dStoreData,
            () => { Debug.Log("Got Game data successfully"); }
        );
    }



    #region Single Player Session Functions

    public void ReInitializeSPSession(string GameId)
    {
        points = 0;
        duration = 0;
        bIsPaused = false;
        ActionAndGameInfoManager.SetYipliGameInfo(GameId);
    }

    public IDictionary<YipliUtils.PlayerActions, int> getPlayerActionCounts()
    {
        return playerActionCounts;
    }
    public Dictionary<string, dynamic> GetPlayerSessionDataJsonDic()
    {
        Dictionary<string, dynamic> x;
        x = new Dictionary<string, dynamic>();
        x.Add("game-id", gameId);
        x.Add("user-id", userId);
        x.Add("mat-id", matId);
        x.Add("mac-address", matMacAddress);
        x.Add("player-id", playerId);
        x.Add("age", int.Parse(playerAge));
        x.Add("points", (int)points);
        x.Add("height", playerHeight);
        x.Add("duration", (int)duration);
        x.Add("intensity", intensityLevel);
        x.Add("player-actions", playerActionCounts);
        x.Add("timestamp", ServerValue.Timestamp);
        x.Add("calories", (int)calories);
        x.Add("fitness-points", (int)fitnesssPoints);
        if (playerGameData != null)
        {
            if (playerGameData.Count > 0)
            {
                x.Add("game-data", playerGameData);
            }
            else
            {
                Debug.Log("Game-data is empty");
            }
        }
        else
        {
            Debug.Log("Game-data is null");
        }

        return x;
    }
    public void StartSPSession(string GameId)
    {
        Debug.Log("Starting current player session.");
        userId = currentYipliConfig.userId ?? "";
        playerId = currentYipliConfig.playerInfo.playerId;
        playerAge = currentYipliConfig.playerInfo.playerAge ?? "";
        playerHeight = currentYipliConfig.playerInfo.playerHeight ?? "";
        playerWeight = currentYipliConfig.playerInfo.playerWeight ?? "";
        playerActionCounts = new Dictionary<YipliUtils.PlayerActions, int>();
        points = 0;
        duration = 0;
        bIsPaused = false;
        ActionAndGameInfoManager.SetYipliGameInfo(GameId);
        matId = currentYipliConfig.matInfo.matId;
        matMacAddress = currentYipliConfig.matInfo.macAddress;
    }
    public void StoreSPSession(float gamePoints)
    {
        Debug.Log("Storing current player session to backend database.");
        points = gamePoints;

        calories = YipliUtils.GetCaloriesBurned(getPlayerActionCounts());
        fitnesssPoints = YipliUtils.GetFitnessPoints(getPlayerActionCounts());
        xp = YipliUtils.GetXP(Math.Ceiling(duration));

        if (0 == ValidateSessionBeforePosting())
        {
            //Store the session data to backend.
            FirebaseDBHandler.PostPlayerSession(Instance, () => { Debug.Log("Session stored in db"); });
            Debug.Log("Single player session stored successfully.");
        }
        else
        {
            Debug.Log("Session not posted : Validation failed for sessoin data.");
        }
    }
    private int ValidateSessionBeforePosting()
    {
        if (gameId == null || gameId == "")
        {
            Debug.Log("gameId is not set");
            return -1;  
        }
        if (playerId == null || playerId == "")
        {
            Debug.Log("playerId is not set");
            return -1;
        }
        if (playerActionCounts.Count == 0)
        {
            Debug.Log("playerActionCounts is not set");
            return -1;
        }
        if (duration == 0)
        {
            Debug.Log("duration is 0");
            return -1;
        }
        if (intensityLevel == "")
        {
            Debug.Log("intensityLevel is not set");
            return -1;
        }
        return 0;
    }
    public void PauseSPSession()
    {
        Debug.Log("Pausing current player session.");
        bIsPaused = true; // only set the paused flat to true. Fixed update will take care of halting the time counter
                          //Ble check
        if (!YipliHelper.GetBleConnectionStatus().Equals("Connected", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("In PauseSPSession : Ble disconnected");
            if (!BleErrorPanel.activeSelf)
            {
                FindObjectOfType<YipliAudioManager>().Play("BLE_failure");
                BleErrorPanel.SetActive(true);
            }
        }
    }
    public void ResumeSPSession()
    {
        Debug.Log("Resuming current player session.");
        bIsPaused = false;
    }
    public void AddPlayerAction(YipliUtils.PlayerActions action, int count = 1)
    {
        Debug.Log("Adding action in current player session.");
        if (playerActionCounts.ContainsKey(action))
            playerActionCounts[action] = playerActionCounts[action] + count;
        else
            playerActionCounts.Add(action, count);
    }

    #endregion

    #region Multi Player Session Functions

    public IDictionary<YipliUtils.PlayerActions, int> getMultiPlayerActionCounts(PlayerDetails playerDetails)
    {
        return playerDetails.playerActionCounts;
    }
    public Dictionary<string, dynamic> GetMultiPlayerSessionDataJsonDic(PlayerDetails playerDetails, string mpSessionUUID)
    {
        Debug.Log("UUID= " + mpSessionUUID);

        Dictionary<string, dynamic> x;
        x = new Dictionary<string, dynamic>();
        x.Add("game-id", playerDetails.gameId);
        x.Add("user-id", playerDetails.userId);
        x.Add("mat-id", playerDetails.matId);
        x.Add("mac-address", playerDetails.matMacAddress);
        x.Add("player-id", playerDetails.playerId);
        x.Add("age", int.Parse(playerDetails.playerAge));
        x.Add("points", (int)playerDetails.points);
        x.Add("height", playerDetails.playerHeight);
        x.Add("duration", (int)playerDetails.duration);
        x.Add("intensity", playerDetails.intensityLevel);
        x.Add("player-actions", playerDetails.playerActionCounts);
        x.Add("timestamp", ServerValue.Timestamp);
        x.Add("mp-session-id", mpSessionUUID);
        x.Add("calories", (int)playerDetails.calories);
        x.Add("fitness-points", (int)playerDetails.fitnesssPoints);
        x.Add("xp", xp);
        if (playerGameData != null)
        {
            if (playerGameData.Count > 0)
            {
                x.Add("game-data", playerGameData);
            }
            else
            {
                Debug.Log("Game-data is empty");
            }
        }
        else
        {
            Debug.Log("Game-data is null");
        }

        return x;
    }
    public void StartMPSession(string GameId)
    {
        Debug.Log("Starting multi player session.");
        userId = currentYipliConfig.userId ?? "";
        playerActionCounts = new Dictionary<YipliUtils.PlayerActions, int>();

        ActionAndGameInfoManager.SetYipliMultiplayerGameInfo(GameId);

        currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails.points = 0;
        currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails.points = 0;
        currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails.duration = 0;
        currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails.duration = 0;

        duration = 0;

        bIsPaused = false;


        matId = currentYipliConfig.matInfo.matId;
        matMacAddress = currentYipliConfig.matInfo.macAddress;

    }
    public void StoreMPSession(float playerOneGamePoints, float playerTwoGamePoints)
    {
        Debug.LogError("Duration is " + duration);
        currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails.duration = duration;
        currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails.duration = duration;

        Debug.Log("Storing current player session to backend database.");

        currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails.calories = YipliUtils.GetCaloriesBurned(getMultiPlayerActionCounts(currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails));
        currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails.fitnesssPoints = YipliUtils.GetFitnessPoints(getMultiPlayerActionCounts(currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails));

        currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails.calories = YipliUtils.GetCaloriesBurned(getMultiPlayerActionCounts(currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails));
        currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails.fitnesssPoints = YipliUtils.GetFitnessPoints(getMultiPlayerActionCounts(currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails));

        currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails.points = playerOneGamePoints;
        currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails.points = playerTwoGamePoints;

        string mpSessionUUID = Guid.NewGuid().ToString();

        if (0 == ValidateMPSessionBeforePosting(currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails))
        {
            //Store the session data to backend.
            FirebaseDBHandler.PostMultiPlayerSession(Instance, currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails, mpSessionUUID, () => { Debug.Log("Session stored in db"); });
            Debug.Log("Player 1 session stored successfully.");
        }
        else
        {
            Debug.Log("Session not posted : Validation failed for sessoin data.");
        }

        if (0 == ValidateMPSessionBeforePosting(currentYipliConfig.MP_GameStateManager.playerData.PlayerTwoDetails))
        {
            //Store the session data to backend.
            FirebaseDBHandler.PostMultiPlayerSession(Instance, currentYipliConfig.MP_GameStateManager.playerData.PlayerOneDetails, mpSessionUUID, () => { Debug.Log("Session stored in db"); });
            Debug.Log("Player 2 session stored successfully.");
        }
        else
        {
            Debug.Log("Session not posted : Validation failed for sessoin data.");
        }
    }
    private int ValidateMPSessionBeforePosting(PlayerDetails playerDetails)
    {
        if (playerDetails.gameId == null || playerDetails.gameId == "")
        {
            Debug.Log("gameId is not set");
            return -1;
        }
        if (playerDetails.playerId == null || playerDetails.playerId == "")
        {
            Debug.Log("playerId is not set");
            return -1;
        }
        if (playerDetails.playerActionCounts.Count == 0)
        {
            Debug.Log("playerActionCounts is not set");
            return -1;
        }
        if (playerDetails.duration == 0)
        {
            Debug.Log("duration is 0");
            return -1;
        }
        return 0;
    }
    public void PauseMPSession()
    {
        PauseSPSession();
    }
    public void ResumeMPSession()
    {
        ResumeSPSession();
    }
    public void AddMultiPlayerAction(YipliUtils.PlayerActions action, PlayerDetails playerDetails, int count = 1)
    {
        Debug.Log("Adding action in current player session.");
        if (playerDetails.playerActionCounts.ContainsKey(action))
            playerDetails.playerActionCounts[action] = playerDetails.playerActionCounts[action] + count;
        else
            playerDetails.playerActionCounts.Add(action, count);
    }

    #endregion
}
