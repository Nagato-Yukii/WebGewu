using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TinkercoinFallCounter : MonoBehaviour
{
    public static int TotalFalls { get; private set; }
    public static int TotalCoins { get; private set; }

    public static event Action CountersChanged;

    private static readonly Dictionary<int, int> s_FallsPerAgentId = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> s_CoinsPerAgentId = new Dictionary<int, int>();

    [Header("UI")]
    public Text text;
    public KeyCode resetKey = KeyCode.F9;

    public static void ReportFall(TinkercoinAgent agent)
    {
        TotalFalls++;

        if (agent != null)
        {
            int id = agent.agentId;
            if (s_FallsPerAgentId.TryGetValue(id, out var n))
            {
                s_FallsPerAgentId[id] = n + 1;
            }
            else
            {
                s_FallsPerAgentId[id] = 1;
            }
        }

        CountersChanged?.Invoke();
    }

    public static int GetFallsForAgentId(int agentId)
    {
        return s_FallsPerAgentId.TryGetValue(agentId, out var n) ? n : 0;
    }

    public static void ReportCoinCollected(TinkercoinAgent agent)
    {
        TotalCoins++;

        if (agent != null)
        {
            int id = agent.agentId;
            if (s_CoinsPerAgentId.TryGetValue(id, out var n))
            {
                s_CoinsPerAgentId[id] = n + 1;
            }
            else
            {
                s_CoinsPerAgentId[id] = 1;
            }
        }

        CountersChanged?.Invoke();
    }

    public static int GetCoinsForAgentId(int agentId)
    {
        return s_CoinsPerAgentId.TryGetValue(agentId, out var n) ? n : 0;
    }

    public static void ResetAll()
    {
        TotalFalls = 0;
        TotalCoins = 0;
        s_FallsPerAgentId.Clear();
        s_CoinsPerAgentId.Clear();
        CountersChanged?.Invoke();
    }

    void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            ResetAll();
        }

        if (text != null)
        {
            text.text = BuildDisplayText();
        }
    }

    private static string BuildDisplayText()
    {
        return $"\u6454\u5012\u3010{TotalFalls}\u3011\u6b21\uff0c\u83b7\u5f97\u91d1\u5e01\u3010{TotalCoins}\u3011\u679a";
    }
}
