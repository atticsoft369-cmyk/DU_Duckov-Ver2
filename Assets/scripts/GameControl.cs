using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 게임 루트 폴더의 GameControl.csv 로 밸런스를 조정합니다. (빌드 후에도 수정 가능)
/// - 에디터: 프로젝트 폴더(Assets와 같은 위치)/GameControl.csv
/// - 빌드  : 실행 파일(.exe)과 같은 폴더/GameControl.csv
/// 파일이 없거나 값이 비어 있으면 기존 설정(Inspector / Mob_Info / MobTable)을 그대로 사용합니다.
///
/// 형식 (엑셀에서 편집 가능)
///   [PLAYER]   key,value,설명
///   [MONSTER]  mob,hp,speed,atk,설명
///   [BOSS]     mob,hp,speed,atk1,atk2,설명
///   [WAVE]     MobTable과 같은 열 구성 (id,level,delay1,mob1_1,value1_1,mob1_2,value1_2,... ,wavetimer)
/// </summary>
public static class GameControl
{
    public const string FileName = "GameControl.csv";

    public class MonsterStat
    {
        public string mob;
        public float hp = -1f;
        public float speed = -1f;
        public float atk = -1f;
    }

    public class BossStat
    {
        public string mob;
        public float hp = -1f;
        public float speed = -1f;
        public float atk1 = -1f;
        public float atk2 = -1f;
    }

    static bool loaded;
    static readonly Dictionary<string, float> player = new Dictionary<string, float>();
    static readonly Dictionary<string, MonsterStat> monsters = new Dictionary<string, MonsterStat>();
    static readonly Dictionary<string, BossStat> bosses = new Dictionary<string, BossStat>();
    static string waveCsv;

    public static string FilePath =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", FileName);

    public static bool HasFile { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        loaded = false;
        player.Clear();
        monsters.Clear();
        bosses.Clear();
        waveCsv = null;
        HasFile = false;
    }

    static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        string path = FilePath;

        if (!File.Exists(path))
        {
            Debug.Log("[GameControl] 파일 없음 → 기본 설정 사용: " + path);
            return;
        }

        string text;

        try
        {
            // 엑셀이 열어 둔 상태에서도 읽을 수 있게 공유 모드로 엽니다.
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8, true))
            {
                text = sr.ReadToEnd();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GameControl] 읽기 실패: " + path + "\n" + e.Message);
            return;
        }

        HasFile = true;
        Parse(text);
        Debug.Log($"[GameControl] 로드 완료: {path} / PLAYER {player.Count}개, MONSTER {monsters.Count}개, BOSS {bosses.Count}개, WAVE {(waveCsv != null ? "있음" : "없음")}");
    }

    static void Parse(string text)
    {
        string section = "";
        bool expectHeader = false;
        StringBuilder wave = null;

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (string raw in lines)
        {
            string line = raw.TrimStart('﻿');
            string[] cols = line.Split(',');
            string first = cols.Length > 0 ? cols[0].Trim() : "";

            if (first.StartsWith("[") && first.EndsWith("]"))
            {
                section = first.Trim('[', ']').Trim().ToUpperInvariant();
                expectHeader = section != "PLAYER";

                if (section == "WAVE")
                {
                    wave = new StringBuilder();
                }

                continue;
            }

            if (IsEmptyRow(cols) || first.StartsWith("#") || first.StartsWith("//"))
            {
                continue;
            }

            switch (section)
            {
                case "PLAYER":
                    if (cols.Length >= 2 && TryFloat(cols[1], out float v))
                    {
                        player[first.ToLowerInvariant()] = v;
                    }
                    break;

                case "MONSTER":
                    if (expectHeader) { expectHeader = false; break; }
                    monsters[first] = new MonsterStat
                    {
                        mob = first,
                        hp = Col(cols, 1),
                        speed = Col(cols, 2),
                        atk = Col(cols, 3)
                    };
                    break;

                case "BOSS":
                    if (expectHeader) { expectHeader = false; break; }
                    bosses[first] = new BossStat
                    {
                        mob = first,
                        hp = Col(cols, 1),
                        speed = Col(cols, 2),
                        atk1 = Col(cols, 3),
                        atk2 = Col(cols, 4)
                    };
                    break;

                case "WAVE":
                    // 헤더 포함 그대로 MobTable 형식으로 넘깁니다.
                    wave.Append(line).Append('\n');
                    break;
            }
        }

        if (wave != null && wave.ToString().Split('\n').Length > 2)
        {
            waveCsv = wave.ToString();
        }
    }

    static bool IsEmptyRow(string[] cols)
    {
        foreach (string c in cols)
        {
            if (!string.IsNullOrWhiteSpace(c)) return false;
        }
        return true;
    }

    static float Col(string[] cols, int i)
    {
        return i < cols.Length && TryFloat(cols[i], out float v) ? v : -1f;
    }

    static bool TryFloat(string s, out float v)
    {
        return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    // ---------------- 조회 ----------------

    /// <summary>[PLAYER] 값. 없으면 fallback.</summary>
    public static float GetPlayer(string key, float fallback)
    {
        EnsureLoaded();
        return player.TryGetValue(key.ToLowerInvariant(), out float v) ? v : fallback;
    }

    public static int GetPlayerInt(string key, int fallback)
    {
        return Mathf.RoundToInt(GetPlayer(key, fallback));
    }

    public static MonsterStat GetMonster(string mob)
    {
        EnsureLoaded();
        return mob != null && monsters.TryGetValue(mob, out MonsterStat s) ? s : null;
    }

    public static BossStat GetBoss(string mob)
    {
        EnsureLoaded();
        return mob != null && bosses.TryGetValue(mob, out BossStat s) ? s : null;
    }

    /// <summary>[WAVE] 섹션을 MobTable.csv 형식 텍스트로 반환. 없으면 null.</summary>
    public static string GetWaveCsv()
    {
        EnsureLoaded();
        return waveCsv;
    }
}
