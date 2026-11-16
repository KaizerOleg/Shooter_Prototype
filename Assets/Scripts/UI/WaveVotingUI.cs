
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WaveVotingUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private GameObject votingPanel;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text votesText;
    [SerializeField] private TMP_Text statusText;

    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private int observedVoteRound = -1;
    private bool hasVoted;

    private void Awake()
    {
        if (votingPanel != null)
            votingPanel.SetActive(false);

        if (yesButton != null)
            yesButton.onClick.AddListener(VoteYes);

        if (noButton != null)
            noButton.onClick.AddListener(VoteNo);
    }

    private void Update()
    {
        if (waveManager == null)
        {
            SetPanelVisible(false);
            return;
        }

        if (!waveManager.IsSpawned)
        {
            SetPanelVisible(false);
            return;
        }

        bool voting =
            waveManager.State.Value ==
            WaveManager.WaveState.WaitingForVotes;

        SetPanelVisible(voting);

        if (voting)
        {
            Debug.Assert(
                votingPanel != null,
                "[WaveVotingUI] Не призначено Voting Panel в Inspector!"
            );

            if (titleText != null)
                titleText.text = "Хвилю завершено!";

            if (waveText != null)
                waveText.text =
                    $"Завершена хвиля: {waveManager.CurrentWave.Value}";

            if (enemiesText != null)
                enemiesText.text =
                    $"Ворогів залишилось: {waveManager.AliveEnemies.Value}";

            if (votesText != null)
                votesText.text =
                    $"Голосів «За»: {waveManager.YesVotes.Value} / " +
                    $"{waveManager.EligibleVoters.Value}";

            if (statusText != null)
                statusText.text = hasVoted
                    ? "Твій голос враховано. Очікуємо команду..."
                    : "Чи готові перейти до наступної хвилі?";

            if (yesButton != null)
                yesButton.interactable = !hasVoted;

            if (noButton != null)
                noButton.interactable = !hasVoted;
        }
        else
        {
            observedVoteRound = waveManager.VoteRound.Value;
            hasVoted = false;
        }
    }

    private void VoteYes()
    {
        CastVote(true);
    }

    private void VoteNo()
    {
        CastVote(false);
    }

    private void CastVote(bool voteYes)
    {
        if (waveManager == null ||
            !waveManager.IsSpawned ||
            waveManager.State.Value !=
            WaveManager.WaveState.WaitingForVotes ||
            hasVoted)
            return;

        waveManager.SubmitVote(voteYes);
        hasVoted = true;
    }

    private void SetPanelVisible(bool visible)
    {
        if (votingPanel != null &&
            votingPanel.activeSelf != visible)
        {
            votingPanel.SetActive(visible);
        }
    }

    private void OnDestroy()
    {
        if (yesButton != null)
            yesButton.onClick.RemoveListener(VoteYes);

        if (noButton != null)
            noButton.onClick.RemoveListener(VoteNo);
    }
}
