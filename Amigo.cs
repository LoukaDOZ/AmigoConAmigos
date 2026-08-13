using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using BepInEx;
using UnityEngine.Events;
using Extensions;
using FMODUnity;
using UnityEngine.Scripting;

namespace MoreGamesBase
{
    public class Amigo : GameBase
    {
        public GameObject ResultTelevision;
        public InteractableEventTrigger[] Buttons;
        public InteractableEventTrigger PlayButton;
        public InteractableEventTrigger ResetButton;

        //private readonly static BepInEx.Logging.ManualLogSource LOGGER = BepInEx.Logging.Logger.CreateLogSource("AMIGO");
        private List<int> _selectedButtons = new List<int>();

        private readonly static int maxSelectionCount = 7;
        private readonly static int minVal = 0;
        private readonly static int maxVal = 27;
        private readonly static int blueCount = 7;
        private readonly static int yellowCount = 5;

        private void Start()
        {
            PlayButton.onInteractEvent.AddListener(TryStartGame);
            ResetButton.onInteractEvent.AddListener(ResetSelection);

            for (int i = 0; i < Buttons.Length; i++)
            {
                int b = i;
                ApplyStyleSelectButton(b, false);
                ApplyStyleSelectResult(i, false, false);
                Buttons[i].onInteractEvent.AddListener((_) => OnButtonSelected(b));
            }

            buttonsCurrentBetText = transform.Find("Model/SM_Amigo_machine/Paper/Paper/Bet").gameObject.GetComponent<TextMeshPro>();
            winCurrentGains = transform.Find("Model/SM_Amigo_machine/Paper/PossibleWin/Total").gameObject.GetComponent<TextMeshPro>();
            winSelection = transform.Find("Model/SM_Amigo_machine/Paper/PossibleWin/Selected").gameObject.transform;

            winVFX = transform.Find("Feedbacks/GameWinVFX").gameObject.GetComponent<ParticleSystem>();
            loseVFX = transform.Find("Feedbacks/GameLoseVFX").gameObject.GetComponent<ParticleSystem>();
            tieVFX = transform.Find("Feedbacks/GameTieVFX").gameObject.GetComponent<ParticleSystem>();

            drawNumberText = ResultTelevision.transform.Find("Box/Screen/DrawNumber/Number").gameObject.GetComponent<TextMeshPro>();
            hourText = ResultTelevision.transform.Find("Box/Screen/Time/Hours").gameObject.GetComponent<TextMeshPro>();
            minutesText = ResultTelevision.transform.Find("Box/Screen/Time/Minutes").gameObject.GetComponent<TextMeshPro>();
            ball = ResultTelevision.transform.Find("Box/Screen/Ball").gameObject.transform;
            bar = ResultTelevision.transform.Find("Box/Screen/Bar").gameObject.transform;
            blueBar = ResultTelevision.transform.Find("Box/Screen/Bar/Blue").gameObject;
            yellowBar = ResultTelevision.transform.Find("Box/Screen/Bar/Yellow").gameObject;
            BoxCollider bcBall = ball.GetComponent<BoxCollider>();
            BoxCollider bcBar = bar.GetComponent<BoxCollider>();
            BoxCollider bcBallBounds = ResultTelevision.transform.Find("Box/Screen").gameObject.GetComponent<BoxCollider>();

            ballPlayZone = new Bounds(bcBallBounds.center, bcBallBounds.size);
            barPlayZone = ballPlayZone;
            ballPlayZone.Expand(-new Vector3(bcBall.size.x * ball.localScale.x, bcBall.size.y * ball.localScale.y, 0f));
            barPlayZone.Expand(-new Vector3(bcBar.size.x * bar.localScale.x, 0f, 0f));
            UpdateWinTable();
            ResetTv();
        }

        private void Update()
        {
            DateTime now = DateTime.Now;
            hourText.text = $"{now:HH}";
            minutesText.text = $"{now:mm}";
            buttonsCurrentBetText.text = $"${currentBet}";
        }

        protected override bool CanGameStart()
        {
            return base.CanGameStart() && _selectedButtons.Count == maxSelectionCount;
        }

        public override void OnStartServer()
        {
            gameFeedbacks = GetComponent<CasinoGameFeedbacks>();
            base.OnStartServer();
        }

        int aaa = 0;
        public override void TryStartGame(PlayerInteract playerInteract)
        {
            if (isPlaying)
                return;

            base.TryStartGame(playerInteract);
        }

        [Server]
        protected override void StartGame()
        {
            if (!NetworkServer.active) return;

            base.StartGame();

            List<int> exclude = new List<int>();
            Result[] blues = new Result[blueCount];
            Result[] yellows = new Result[yellowCount];
            int nbGoodBlues = 0;
            int nbGoodYellows = 0;

            for (int i = 0; i < blueCount; i++)
            {
                int v = GetUniqueResult(exclude);
                bool win = false;
                exclude.Add(v);

                if (_selectedButtons.Contains(v))
                {
                    nbGoodBlues += 1;
                    win = true;
                }

                blues[i] = new Result()
                {
                    id = v,
                    win = win
                };
            }

            for (int i = 0; i < yellowCount; i++)
            {
                int v = GetUniqueResult(exclude);
                bool win = false;
                exclude.Add(v);

                if (_selectedButtons.Contains(v))
                {
                    nbGoodYellows += 1;
                    win = true;
                }

                yellows[i] = new Result()
                {
                    id = v,
                    win = win
                };
            }

            ShowResult(blues, yellows, () =>
            {
                Payout(MULTIPLIERS[nbGoodBlues][nbGoodYellows], ChangeType.GameResult, null, currentBet);
                RpcPlayGameResultFeedback(MULTIPLIERS[nbGoodBlues][nbGoodYellows]);
                ResetGame();
            });
        }

        private int GetUniqueResult(List<int> exclude)
        {
            int v = UnityEngine.Random.Range(minVal, maxVal + 1);

            while (exclude.Contains(v))
                v = UnityEngine.Random.Range(minVal, maxVal + 1);

            return v;
        }

        private void ResetSelection(PlayerInteract playerInteract)
        {
            foreach (int b in _selectedButtons.ToArray())
                RpcButtonUnselected(b);
        }

        private void OnButtonSelected(int i)
        {
            if (isPlaying)
                return;

            if (_selectedButtons.Contains(i))
                RpcButtonUnselected(i);
            else if (_selectedButtons.Count < maxSelectionCount)
                RpcButtonSelected(i);
        }

        private static readonly double[][] MULTIPLIERS = new double[8][] {
        // Yellow       0       1       2       3       4       5       Blue
        new double[6] { 0f,     0f,     0f,     0f,     1f,     1.5f   }, // 0
        new double[6] { 0f,     0f,     0f,     1f,     1.5f,   7.5f   }, // 1
        new double[6] { 0f,     0f,     1f,     1.5f,   7.5f,   50f    }, // 2
        new double[6] { 0f,     1f,     1.5f,   7.5f,   50f,    50f    }, // 3
        new double[6] { 2.5f,   4f,     10f,    52.5f,  52.5f,  52.5f  }, // 4
        new double[6] { 20f,    27.5f,  70f,    70f,    70f,    70f    }, // 5
        new double[6] { 250f,   300f,   300f,   300f,   300f,   300f   }, // 6
        new double[6] { 12500f, 12500f, 12500f, 12500f, 12500f, 12500f }, // 7
    };


        /// Buttons <summary>

        private TextMeshPro buttonsCurrentBetText;
        private int numberOfCrosses = 9;

        private void ApplyStyleSelectButton(int i, bool selected)
        {
            int cross = selected ? UnityEngine.Random.Range(1, numberOfCrosses + 1) : -1;

            for (int j = 1; j <= numberOfCrosses; j++)
                Buttons[i].transform.Find("Model/Result/SelectionCross/Cross" + j).gameObject.SetActive(j == cross);
        }

        [ClientRpc]
        private void RpcButtonSelected(int i)
        {
            if (!NetworkClient.active) return;

            _selectedButtons.Add(i);
            ApplyStyleSelectButton(i, true);
        }

        [ClientRpc]
        private void RpcButtonUnselected(int i)
        {
            if (!NetworkClient.active) return;

            _selectedButtons.Remove(i);
            ApplyStyleSelectButton(i, false);
        }


        /// Results

        private TextMeshPro winCurrentGains;
        private Transform winSelection;

        private void UpdateWinTable()
        {
            winCurrentGains.text = $"${currentBet}";

            for (int b = 0; b < blueCount + 1; b++)
            {
                int maxY = Math.Clamp(7 - b, 0, 5);

                for (int y = 0; y <= maxY; y++)
                {
                    if (b + y < 4)
                        continue;

                    transform.Find($"Model/SM_Amigo_machine/Paper/PossibleWin/Good{b + y}/Line{b}{y}/Win").gameObject.GetComponent<TextMeshPro>().text = $"x{MULTIPLIERS[b][y]}";
                }
            }
        }

        private void RpcUpdateSelectedWin(bool show, int nbBlue = -1, int nbYellow = -1)
        {
            if (!NetworkClient.active) return;

            winSelection.gameObject.SetActive(show && nbBlue + nbYellow >= 4);

            if (nbBlue != -1 && nbYellow != -1)
            {
                winCurrentGains.text = "$" + (currentBet * MULTIPLIERS[nbBlue][nbYellow]).ToString();

                if (nbBlue + nbYellow >= 4)
                {
                    int ny = nbYellow % (blueCount - nbBlue + 1);
                    Vector3 tmp = winSelection.localPosition;
                    winSelection.parent = transform.Find($"Model/SM_Amigo_machine/Paper/PossibleWin/Good{nbBlue + ny}/Line{nbBlue}{ny}").gameObject.transform;
                    winSelection.localPosition = tmp;
                }
            }
            else
                winCurrentGains.text = "$0";
        }

        private void ApplyStyleSelectResult(int i, bool selected, bool blue)
        {
            Results[i].transform.Find("Model/Result/Default").gameObject.SetActive(!selected);
            Results[i].transform.Find("Model/Result/Blue").gameObject.SetActive(selected && blue);
            Results[i].transform.Find("Model/Result/Yellow").gameObject.SetActive(selected && !blue);
        }

        [ClientRpc]
        private void RpcSelectResult(int i, bool isSelected, bool blue)
        {
            if (!NetworkClient.active) return;

            ApplyStyleSelectResult(i, isSelected, blue);
        }

        /// ResultTelevision

        public GameObject[] Results;
        public float breakDelay = .5f;
        public float ballSpeed = 3f;
        public float minMaxAngle = 10f;
        public int minBounce = 6;
        public int maxBounce = 12;
        public int maxRequestBounce = 8;
        private Transform ball;
        private Transform bar;
        private GameObject blueBar;
        private GameObject yellowBar;
        private Bounds ballPlayZone;
        private Bounds barPlayZone;
        private TextMeshPro hourText;
        private TextMeshPro minutesText;
        private TextMeshPro drawNumberText;
        private int currentDraw = 0;

        private void ShowResult(Result[] blueResults, Result[] yellowResults, Action callback)
        {
            BallPath[] bluePaths = new BallPath[blueResults.Length];
            BallPath[] yellowPaths = new BallPath[yellowResults.Length];

            drawNumberText.text = (++currentDraw).ToString();

            for (int i = 0; i < blueResults.Length; i++)
            {
                while (true)
                {
                    bluePaths[i] = new BallPath()
                    {
                        result = blueResults[i],
                        path = BuildBallPath(Results[blueResults[i].id], out bool success)
                    };

                    if (success)
                        break;
                }
            }

            for (int i = 0; i < yellowResults.Length; i++)
            {
                while (true)
                {
                    yellowPaths[i] = new BallPath()
                    {
                        result = yellowResults[i],
                        path = BuildBallPath(Results[yellowResults[i].id], out bool success)
                    };

                    if (success)
                        break;
                }
            }

            StartCoroutine(ShowResultRoutine(bluePaths, yellowPaths, callback));
        }

        private Vector3[] BuildBallPath(GameObject result, out bool success)
        {
            float angle = UnityEngine.Random.Range(minMaxAngle, 90f - minMaxAngle);
            float inverseX = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
            float inverseY = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
            Vector3 currentDirection = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * inverseX, Mathf.Cos(angle * Mathf.Deg2Rad) * inverseY, 0).normalized;

            int minSize = UnityEngine.Random.Range(minBounce, maxRequestBounce);
            List<Vector3> path = new List<Vector3>();
            Vector3 currentPosition = result.transform.localPosition;
            path.Add(currentPosition);

            while (true)
            {
                Vector3 inverseDir = currentDirection * -1;
                float timeToLeft = (ballPlayZone.min.x - currentPosition.x) * inverseDir.x > 0.001f ? Mathf.Abs((currentPosition.x - ballPlayZone.min.x) / (inverseDir.x * ballSpeed)) : float.PositiveInfinity;
                float timeToRight = (ballPlayZone.max.x - currentPosition.x) * inverseDir.x > 0.001f ? Mathf.Abs((ballPlayZone.max.x - currentPosition.x) / (inverseDir.x * ballSpeed)) : float.PositiveInfinity;
                float timeToTop = (ballPlayZone.min.y - currentPosition.y) * inverseDir.y > 0.001f ? Mathf.Abs((currentPosition.y - ballPlayZone.min.y) / (inverseDir.y * ballSpeed)) : float.PositiveInfinity;
                float timeToBottom = (ballPlayZone.max.y - currentPosition.y) * inverseDir.y > 0.001f ? Mathf.Abs((ballPlayZone.max.y - currentPosition.y) / (inverseDir.y * ballSpeed)) : float.PositiveInfinity;

                float minTimeX = Mathf.Min(timeToLeft, timeToRight);
                float minTimeY = Mathf.Min(timeToTop, timeToBottom);
                float minTime = Mathf.Min(minTimeX, minTimeY);
                currentPosition = new Vector3(
                    Mathf.Clamp(currentPosition.x + inverseDir.x * ballSpeed * minTime, ballPlayZone.min.x, ballPlayZone.max.x),
                    Mathf.Clamp(currentPosition.y + inverseDir.y * ballSpeed * minTime, ballPlayZone.min.y, ballPlayZone.max.y),
                    currentPosition.z
                );

                if (minTimeX < minTimeY)
                    currentDirection = new Vector3(currentDirection.x * -1, currentDirection.y, currentDirection.z);
                else if (minTimeX > minTimeY)
                    currentDirection = new Vector3(currentDirection.x, currentDirection.y * -1, currentDirection.z);
                else
                    currentDirection = new Vector3(currentDirection.x * -1, currentDirection.y * -1, currentDirection.z);

                path.Insert(0, currentPosition);

                if (path.Count >= maxBounce)
                {
                    success = false;
                    break;
                }

                if (path.Count >= minSize && minTimeY < minTimeX && timeToBottom > timeToTop)
                {
                    success = true;
                    break;
                }
            }

            return path.ToArray();
        }

        private IEnumerator ShowResultRoutine(BallPath[] bluePaths, BallPath[] yellowPaths, Action callback)
        {
            int nbGoodBlue = 0;
            int nbGoodYellow = 0;

            foreach (BallPath p in bluePaths)
            {
                RpcMoveBall(p.path[0]);
                RpcMoveBar(ball.localPosition.x, true);
                RpcSetTvElementsVisible(true, true, false);
                yield return new WaitForSeconds(breakDelay);

                for (int i = 1; i < p.path.Length; i++)
                {
                    Vector3 target = p.path[i];

                    while (Vector3.Distance(ball.localPosition, target) > 0.001f)
                    {
                        RpcMoveBall(Vector3.MoveTowards(ball.localPosition, target, ballSpeed * Time.deltaTime));
                        RpcMoveBar(ball.localPosition.x, true);
                        yield return null;
                    }
                }

                nbGoodBlue += p.result.win ? 1 : 0;
                RpcUpdateSelectedWin(true, nbGoodBlue, nbGoodYellow);
                RpcSelectResult(p.result.id, true, true);
                RpcSetTvElementsVisible(false, true, false);
                yield return new WaitForSeconds(breakDelay);
            }

            yield return new WaitForSeconds(breakDelay);

            foreach (BallPath p in yellowPaths)
            {
                RpcMoveBall(p.path[0]);
                RpcMoveBar(ball.localPosition.x, false);
                RpcSetTvElementsVisible(true, false, true);
                yield return new WaitForSeconds(breakDelay);

                for (int i = 1; i < p.path.Length; i++)
                {
                    Vector3 target = p.path[i];

                    while (Vector3.Distance(ball.localPosition, target) > 0.001f)
                    {
                        RpcMoveBall(Vector3.MoveTowards(ball.localPosition, target, ballSpeed * Time.deltaTime));
                        RpcMoveBar(ball.localPosition.x, false);
                        yield return null;
                    }
                }

                nbGoodYellow += p.result.win ? 1 : 0;
                RpcUpdateSelectedWin(true, nbGoodBlue, nbGoodYellow);
                RpcSelectResult(p.result.id, true, false);
                RpcSetTvElementsVisible(false, false, true);
                yield return new WaitForSeconds(breakDelay);
            }

            yield return new WaitForSeconds(breakDelay * 2);
            callback();
            ResetTv();
        }

        public void ResetTv()
        {
            RpcUpdateSelectedWin(false);
            RpcSetTvElementsVisible(false, false, false);

            for (int i = 0; i < Results.Length; i++)
                RpcSelectResult(i, false, false);
        }

        [ClientRpc]
        public void RpcSetTvElementsVisible(bool ballVisible = false, bool blueBarVisible = false, bool yellowBarVisible = false)
        {
            if (!NetworkClient.active) return;

            ball.gameObject.SetActive(ballVisible);
            blueBar.SetActive(blueBarVisible);
            yellowBar.SetActive(yellowBarVisible);
        }

        [ClientRpc]
        public void RpcMoveBall(Vector3 pos)
        {
            if (!NetworkClient.active) return;

            ball.localPosition = pos;
        }

        [ClientRpc]
        public void RpcMoveBar(float x, bool blue)
        {
            if (!NetworkClient.active) return;

            bar.localPosition = new Vector3(Mathf.Clamp(x, barPlayZone.min.x, barPlayZone.max.x), bar.localPosition.y, bar.localPosition.z);
        }

        private class Result
        {
            public int id;
            public bool win;
        }

        private class BallPath
        {
            public Result result;
            public Vector3[] path;
        }

        // Feedbacks

        private ParticleSystem winVFX;
        private ParticleSystem loseVFX;
        private ParticleSystem tieVFX;
        [SerializeField]
        private EventReference winSFX;

        [SerializeField]
        private EventReference loseSFX;

        [SerializeField]
        private EventReference tieSFX;

        [ClientRpc]
        private void RpcPlayGameResultFeedback(double multiplier)
        {
            if (multiplier < 1.0)
            {
                loseVFX.Play(true);
                SFXManager.SFXOneShot(winSFX, transform.position);
            }
            else if (multiplier > 1.0)
            {
                winVFX.Play(true);
                SFXManager.SFXOneShot(loseSFX, transform.position);
            }
            else
            {
                tieVFX.Play(true);
                SFXManager.SFXOneShot(tieSFX, transform.position);
            }
        }
    }
}

namespace AmigoMod
{
    [BepInPlugin("com.kit2soin.amigo", "Amigo", "1.0.0")]
    [BepInDependency("com.moregames.base", BepInDependency.DependencyFlags.HardDependency)]
    public class AmigoPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("Amigo Assembly loaded by BepInEx! Waiting for Base Loader...");
        }
    }
}