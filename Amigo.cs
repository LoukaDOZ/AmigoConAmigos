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
using BepInEx.Logging;

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
            PlayButton.serverOnInteractEvent.AddListener(TryStartGame);
            ResetButton.serverOnInteractEvent.AddListener(ResetSelection);

            for (int i = 0; i < Buttons.Length; i++)
            {
                int b = i;
                ApplyStyleSelectButton(b, false);
                ApplyStyleSelectResult(i, false, false);
                Buttons[i].serverOnInteractEvent.AddListener((_) => OnButtonSelected(b));
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
            InitWinTable();
            ResetGame();
        }

        private void Update()
        {
            DateTime now = DateTime.Now;
            hourText.text = $"{now:HH}";
            minutesText.text = $"{now:mm}";
            buttonsCurrentBetText.text = $"${currentBet}";
            drawNumberText.text = gameTurn.ToString();
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

        public override void TryStartGame(PlayerInteract playerInteract)
        {
            if (isPlaying)
                return;

            base.TryStartGame(playerInteract);
        }

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
                BroadcastPlayGameResultFeedback(MULTIPLIERS[nbGoodBlues][nbGoodYellows]);
                BroadcastResetGame();
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
            if (isPlaying)
                return;

            BroadcastResetSelection();
        }

        private void OnButtonSelected(int i)
        {
            if (isPlaying)
                return;

            BroadcastButtonSelected(i);
        }

        private void BroadcastButtonSelected(int i)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcButtonSelected(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString(i.ToString());
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        private void UserRpcButtonSelected(string stri)
        {
            if (!NetworkClient.active) return;

            int i = int.Parse(stri);

            if (_selectedButtons.Contains(i))
            {
                _selectedButtons.Remove(i);
                ApplyStyleSelectButton(i, false);
            }
            else if (_selectedButtons.Count < maxSelectionCount)
            {
                _selectedButtons.Add(i);
                ApplyStyleSelectButton(i, true);
            }
        }

        private void BroadcastResetSelection()
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcResetSelection(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString("");
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        private void UserRpcResetSelection(string _)
        {
            if (!NetworkClient.active) return;

            foreach (int b in _selectedButtons.ToArray())
                ApplyStyleSelectButton(b, false);

            _selectedButtons.Clear();
        }

        protected override void ResetGame()
        {
            if (NetworkServer.active)
                base.ResetGame();

            UpdateResultTable();
            SetTvElementVisible(false, false, false);

            for (int i = 0; i < Results.Length; i++)
                ApplyStyleSelectResult(i, false, false);
        }

        private void BroadcastResetGame()
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcResetGame(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString("");
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        private void UserRpcResetGame(string _)
        {
            if (!NetworkClient.active) return;

            ResetGame();
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


        /// Results

        private TextMeshPro winCurrentGains;
        private Transform winSelection;

        private void InitWinTable()
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

        private void UpdateResultTable(int nbBlue = -1, int nbYellow = -1)
        {
            if (nbBlue != -1 && nbYellow != -1)
            {
                winCurrentGains.text = "$" + (currentBet * MULTIPLIERS[nbBlue][nbYellow]).ToString();

                if (nbBlue + nbYellow >= 4)
                {
                    int ny = nbYellow % (blueCount - nbBlue + 1);
                    Vector3 tmp = winSelection.localPosition;
                    winSelection.parent = transform.Find($"Model/SM_Amigo_machine/Paper/PossibleWin/Good{nbBlue + ny}/Line{nbBlue}{ny}").gameObject.transform;
                    winSelection.localPosition = tmp;
                    winSelection.gameObject.SetActive(true);
                }
            }
            else
            {
                winSelection.gameObject.SetActive(false);
                winCurrentGains.text = "$0";
            }
        }

        private void BroadcastUpdateResultTable(int goodBlue, int goodYellow)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcUpdateResultTable(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString($"{goodBlue}:{goodYellow}");
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        private void UserRpcUpdateResultTable(string message)
        {
            if (!NetworkClient.active) return;

            string[] data = message.Split(':');
            int goodBlue = int.Parse(data[0]);
            int goodYellow = int.Parse(data[1]);
            UpdateResultTable(goodBlue, goodYellow);
        }

        private void ApplyStyleSelectResult(int i, bool selected, bool blue)
        {
            Results[i].transform.Find("Model/Result/Default").gameObject.SetActive(!selected);
            Results[i].transform.Find("Model/Result/Blue").gameObject.SetActive(selected && blue);
            Results[i].transform.Find("Model/Result/Yellow").gameObject.SetActive(selected && !blue);
        }

        private void BroadcastSelectResult(int i, bool blue)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcSelectResult(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString($"{i}:{blue}");
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        private void UserRpcSelectResult(string message)
        {
            if (!NetworkClient.active) return;

            string[] data = message.Split(':');
            int i = int.Parse(data[0]);
            bool blue = bool.Parse(data[1]);
            ApplyStyleSelectResult(i, true, blue);
        }

        /// ResultTelevision

        public GameObject[] Results;
        public float breakDelay = .5f;
        public float ballSpeed = 3.5f;
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
                BroadcastMoveBall(p.path[0]);
                BroadcastSetTvElementVisible(true, true, false);
                yield return new WaitForSeconds(breakDelay);

                for (int i = 1; i < p.path.Length; i++)
                {
                    Vector3 target = p.path[i];

                    while (Vector3.Distance(ball.localPosition, target) > 0.001f)
                    {
                        BroadcastMoveBall(Vector3.MoveTowards(ball.localPosition, target, ballSpeed * Time.deltaTime));
                        yield return null;
                    }
                }

                nbGoodBlue += p.result.win ? 1 : 0;
                BroadcastUpdateResultTable(nbGoodBlue, nbGoodYellow);
                BroadcastSelectResult(p.result.id, true);
                BroadcastSetTvElementVisible(false, true, false);
                yield return new WaitForSeconds(breakDelay);
            }

            yield return new WaitForSeconds(breakDelay);

            foreach (BallPath p in yellowPaths)
            {
                BroadcastMoveBall(p.path[0]);
                BroadcastSetTvElementVisible(true, false, true);
                yield return new WaitForSeconds(breakDelay);

                for (int i = 1; i < p.path.Length; i++)
                {
                    Vector3 target = p.path[i];

                    while (Vector3.Distance(ball.localPosition, target) > 0.001f)
                    {
                        BroadcastMoveBall(Vector3.MoveTowards(ball.localPosition, target, ballSpeed * Time.deltaTime));
                        yield return null;
                    }
                }

                nbGoodYellow += p.result.win ? 1 : 0;
                BroadcastUpdateResultTable(nbGoodBlue, nbGoodYellow);
                BroadcastSelectResult(p.result.id, false);
                BroadcastSetTvElementVisible(false, false, true);
                yield return new WaitForSeconds(breakDelay);
            }

            yield return new WaitForSeconds(breakDelay);
            callback();
        }

        private void BroadcastSetTvElementVisible(bool ballVisible = false, bool blueBarVisible = false, bool yellowBarVisible = false)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcSetTvElementVisible(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString($"{ballVisible}:{blueBarVisible}:{yellowBarVisible}");
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        public void UserRpcSetTvElementVisible(string message)
        {
            if (!NetworkClient.active) return;

            string[] data = message.Split(':');
            bool ballVisible = bool.Parse(data[0]);
            bool blueBarVisible = bool.Parse(data[1]);
            bool yellowBarVisible = bool.Parse(data[2]);

            SetTvElementVisible(ballVisible, blueBarVisible, yellowBarVisible);
        }

        public void SetTvElementVisible(bool ballVisible = false, bool blueBarVisible = false, bool yellowBarVisible = false)
        {
            ball.gameObject.SetActive(ballVisible);
            blueBar.SetActive(blueBarVisible);
            yellowBar.SetActive(yellowBarVisible);
        }

        private void BroadcastMoveBall(Vector3 pos)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcMoveBall(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString($"{pos.x}:{pos.y}:{pos.z}");
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        public void UserRpcMoveBall(string message)
        {
            if (!NetworkClient.active) return;

            string[] data = message.Split(':');
            float x = float.Parse(data[0]);
            float y = float.Parse(data[1]);
            float z = float.Parse(data[2]);

            MoveBall(new Vector3(x, y, z));
        }

        public void MoveBall(Vector3 pos)
        {
            ball.localPosition = pos;
            bar.localPosition = new Vector3(Mathf.Clamp(pos.x, barPlayZone.min.x, barPlayZone.max.x), bar.localPosition.y, bar.localPosition.z);
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

        private void BroadcastPlayGameResultFeedback(double multiplier)
        {
            if (!NetworkServer.active) return;
            string methodSignature = "System.Void MoreGamesBase.Amigo::UserRpcPlayGameResultFeedback(System.String)";
            int rpcHash = methodSignature.GetStableHashCode();

            NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.WriteString(multiplier.ToString());
            this.SendRPCInternal(methodSignature, rpcHash, writer, 0, true);
            NetworkWriterPool.Return(writer);
        }

        public void UserRpcPlayGameResultFeedback(string message)
        {
            if (!NetworkClient.active) return;

            PlayGameResultFeedback(double.Parse(message));
        }

        private void PlayGameResultFeedback(double multiplier)
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
    [BepInPlugin("com.kit2soin.amigo", "Amigo", "2.0.0")]
    [BepInDependency("com.moregames.base", BepInDependency.DependencyFlags.HardDependency)]
    public class AmigoPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("Amigo Assembly loaded by BepInEx! Waiting for Base Loader...");
        }
    }
}