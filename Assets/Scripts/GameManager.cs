using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central controller for Dead Signal.
/// Tracks survivors, owns the title / win / lose / pause state, the run timer, and updates the HUD.
/// Other scripts talk to it through GameManager.Instance.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("HUD")]
    public Slider energyBar;
    public Slider healthBar;
    public TMP_Text survivorText;
    public TMP_Text objectiveText;
    public TMP_Text hintText;          // short pop-up messages ("Pod locked!")
    public TMP_Text healthValueText;   // "100 / 100" next to the health bar
    public TMP_Text timerText;         // run timer at the top of the screen

    [Header("Panels")]
    public GameObject titlePanel;
    public GameObject pausePanel;
    public GameObject winPanel;
    public GameObject losePanel;
    public TMP_Text loseReasonText;    // text on the lose panel that explains why you lost
    public TMP_Text winStatsText;      // run time + best time on the win panel

    [Header("Time Limit")]
    public float timeLimit = 180f;          // 3:00 to get everyone out
    public float timeWarningAt = 30f;       // timer turns red and warns at this many seconds left

    private const string BestTimeKey = "DeadSignal_BestTime";

    // Static so it survives a scene reload: Restart skips the title screen.
    private static bool skipTitle;

    // Unity 6 doesn't reset static variables when you press Play again (domain reload is off),
    // so reset it ourselves every time the game starts. Otherwise the title would stay skipped.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        skipTitle = false;
    }

    public int TotalSurvivors { get; private set; }
    public int FoundSurvivors { get; private set; }   // following the player OR already safe in the pod
    public int SafeSurvivors { get; private set; }    // dropped off at the escape pod
    public bool IsGameOver { get; private set; }
    public bool IsPaused { get; private set; }
    public bool OnTitleScreen { get; private set; }
    public float RunTime { get; private set; }

    /// <summary>True once every survivor has been picked up at least once (following or safe).</summary>
    public bool AllSurvivorsFound => TotalSurvivors > 0 && FoundSurvivors >= TotalSurvivors;

    /// <summary>True once every survivor is safe inside the pod - time to launch.</summary>
    public bool AllSurvivorsSafe => TotalSurvivors > 0 && SafeSurvivors >= TotalSurvivors;

    /// <summary>True while gameplay should be running (not on the title, not paused, not over).</summary>
    public bool IsPlaying => !IsGameOver && !IsPaused && !OnTitleScreen;

    private Coroutine hintRoutine;
    private bool timeWarningGiven;

    /// <summary>Seconds left on the countdown (never below 0).</summary>
    public float TimeRemaining => Mathf.Max(0f, timeLimit - RunTime);

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f; // reset in case we reloaded the scene while paused
    }

    private void Start()
    {
        pausePanel.SetActive(false);
        winPanel.SetActive(false);
        losePanel.SetActive(false);
        if (hintText != null) hintText.text = "";

        SetObjective($"You have {FormatTime(timeLimit)}. Find the survivors and bring them to the escape pod.");
        UpdateSurvivorUI();
        UpdateTimerUI();

        // Show the title screen on first launch only (Restart goes straight into the game).
        if (skipTitle) titlePanel.SetActive(false);
        else ShowTitle();
    }

    private void Update()
    {
        if (OnTitleScreen)
        {
            // Enter also starts the game, not just the button.
            if (Input.GetKeyDown(KeyCode.Return)) StartGame();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !IsGameOver)
        {
            TogglePause();
        }

        if (IsPlaying)
        {
            RunTime += Time.deltaTime;
            UpdateTimerUI();
            CheckTimeLimit();
        }
    }

    // ---------------- Survivors ----------------

    /// <summary>Called by each Survivor when it spawns so we know how many exist.</summary>
    public void RegisterSurvivor()
    {
        TotalSurvivors++;
        UpdateSurvivorUI();
    }

    /// <summary>Called by a Survivor when it joins the player's line.</summary>
    public void RescueSurvivor()
    {
        FoundSurvivors++;
        UpdateSurvivorUI();

        if (AllSurvivorsFound)
        {
            // Powering the pod sets off the alarm (see EscapePod.PowerOn).
            if (EscapePod.Instance != null) EscapePod.Instance.PowerOn();
        }
        else
        {
            SetObjective("Survivor found! Bring them to the escape pod before a creature drags them away.");
            ShowHint("Survivor rescued!", 1.5f);
        }
    }

    /// <summary>Called by a Survivor when an enemy knocks it out of the line.</summary>
    public void SurvivorLost()
    {
        FoundSurvivors--;
        UpdateSurvivorUI();
        SetObjective("A survivor was dragged away! Go back and find them.");
        ShowHint("SURVIVOR LOST", 2f);
    }

    /// <summary>Called by the escape pod when the player drops off their followers.</summary>
    public void DepositSurvivors(int count)
    {
        SafeSurvivors += count;
        UpdateSurvivorUI();
        if (AllSurvivorsSafe) return; // the pod launches right away

        ShowHint($"{count} survivor(s) safe in the pod!", 2f);
        if (!AllSurvivorsFound)
        {
            SetObjective("Survivors are safe in the pod. Go find the rest!");
        }
    }

    // ---------------- HUD ----------------

    public void UpdateEnergyUI(float normalized)
    {
        if (energyBar != null) energyBar.value = normalized;
    }

    public void UpdateHealthUI(int current, int max)
    {
        if (healthBar != null) healthBar.value = (float)current / max;
        if (healthValueText != null) healthValueText.text = $"{current} / {max}";
    }

    /// <summary>Shows the countdown. Turns red when time is almost up.</summary>
    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        timerText.text = FormatTime(TimeRemaining);
        timerText.color = TimeRemaining <= timeWarningAt ? new Color(1f, 0.3f, 0.3f) : Color.white;
    }

    private void CheckTimeLimit()
    {
        if (!timeWarningGiven && TimeRemaining <= timeWarningAt)
        {
            timeWarningGiven = true;
            ShowHint($"{Mathf.RoundToInt(timeWarningAt)} SECONDS LEFT!", 2f);
            AudioManager.Instance.Play(AudioManager.Instance.alarmSound, 0.6f);
        }

        if (TimeRemaining <= 0f)
        {
            Lose("Time's up. The station went dark for good.");
        }
    }

    /// <summary>Turns seconds into "m:ss" (rounded up, so the countdown shows 0:00 only when time is really out).</summary>
    private static string FormatTime(float seconds)
    {
        int whole = Mathf.CeilToInt(seconds);
        return $"{whole / 60}:{whole % 60:00}";
    }

    private void UpdateSurvivorUI()
    {
        if (survivorText == null) return;
        int following = FoundSurvivors - SafeSurvivors;
        survivorText.text = $"Safe in pod: {SafeSurvivors} / {TotalSurvivors}\nFollowing you: {following}";
    }

    public void SetObjective(string text)
    {
        if (objectiveText != null) objectiveText.text = text;
    }

    /// <summary>Shows a short message in the middle of the screen, then clears it.</summary>
    public void ShowHint(string message, float duration)
    {
        if (hintText == null) return;
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(HintRoutine(message, duration));
    }

    private IEnumerator HintRoutine(string message, float duration)
    {
        hintText.text = message;
        yield return new WaitForSeconds(duration);
        hintText.text = "";
    }

    // ---------------- Game state ----------------

    private void ShowTitle()
    {
        OnTitleScreen = true;
        Time.timeScale = 0f; // freeze the station behind the title screen
        titlePanel.SetActive(true);
    }

    /// <summary>Hooked up to the title screen's Start button.</summary>
    public void StartGame()
    {
        OnTitleScreen = false;
        Time.timeScale = 1f;
        titlePanel.SetActive(false);
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        AudioListener.pause = IsPaused; // also pauses the low-energy hum
        pausePanel.SetActive(IsPaused);
    }

    /// <summary>
    /// Stops gameplay (enemies freeze, player input off) without showing a screen yet.
    /// Used by the escape pod so its launch animation can play first.
    /// </summary>
    public void BeginEnding()
    {
        IsGameOver = true;
    }

    public void Win()
    {
        IsGameOver = true;
        AudioManager.Instance.StopSignalHum();

        // Save the best (lowest) time between runs.
        float best = PlayerPrefs.GetFloat(BestTimeKey, 0f);
        bool newBest = best <= 0f || RunTime < best;
        if (newBest)
        {
            best = RunTime;
            PlayerPrefs.SetFloat(BestTimeKey, best);
            PlayerPrefs.Save();
        }
        if (winStatsText != null)
        {
            winStatsText.text = $"Time: {FormatTime(RunTime)}     Best: {FormatTime(best)}" + (newBest ? "   NEW BEST!" : "");
        }

        winPanel.SetActive(true);
        AudioManager.Instance.Play(AudioManager.Instance.winSound);
    }

    public void Lose(string reason)
    {
        if (IsGameOver) return; // can't lose twice (e.g. energy and health hit zero together)
        IsGameOver = true;
        AudioManager.Instance.StopSignalHum();
        if (loseReasonText != null) loseReasonText.text = reason;
        losePanel.SetActive(true);
        AudioManager.Instance.Play(AudioManager.Instance.gameOverSound);
    }

    // These are hooked up to UI buttons in the Inspector.
    public void RestartGame()
    {
        skipTitle = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // so Quit also works in the editor
#endif
    }
}
