using System;
using System.Collections.Generic;
using UnityEngine;
using static EnemyStateMachine;
using static PositionStateMachine;
using Cinemachine;
using System.Collections;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Manipulate by an external class, not within the class!!
/// </summary>
public class EnemyBattlePawn : BattlePawn, IAttackReceiver
{
    [field: Header("Enemy References")]
    [field: SerializeField] public EnemyStateMachine esm { get; private set; }
    [field: SerializeField] public PositionStateMachine psm { get; private set; }
    //[SerializeField] private ParticleSystem _particleSystem;
    [field: SerializeField] public Transform targetFightingLocation { get; private set; }
    [field: SerializeField] public CinemachineVirtualCamera battleCam { get; private set; }
    //reference to director for stagger implementation
    public EnemyBattlePawnData EnemyData => (EnemyBattlePawnData)Data;
    private Dictionary<Type, List<EnemyAction>> _enemyActions = new Dictionary<Type, List<EnemyAction>>();
    // Events
    public event Action OnEnemyStaggerEvent;
    public TimelineAsset IntroCutscene;
    public TimelineAsset OutroCutscene;
    
    public int currentStaggerHealth { get; set; }
    public int maxStaggerHealth;
    public bool interruptable; // Staggers interrupt attacks, rather than occurring at end of attack

    // References
    private PlayableDirector _director;
    public PlayableDirector Director => _director;
    protected override void Awake()
    { 
        if (Data.GetType() != typeof(EnemyBattlePawnData))
        {
            Debug.LogError($"Enemy Battle Pawn \"{Data.name}\" is set incorrectly");
            return;
        }
        esm = GetComponent<EnemyStateMachine>();
        if (esm == null)
        {
            Debug.LogError($"Enemy Battle Pawn \"{Data.name}\" must have an EnemyStateMachine");
            return;
        }
        psm = GetComponent<PositionStateMachine>();
        if (psm == null)
        {
            Debug.LogError($"Enemy Battle Pawn \"{Data.name}\" must have a PositionStateMachine");
            return;
        }
        _director = GetComponent<PlayableDirector>();
        if (_director == null)
        {
            Debug.LogError($"Enemy Battle Pawn \"{Data.name}\" must have a PlayableDirector");
            return;
        }
        currentStaggerHealth = EnemyData.StaggerHealth;
        maxStaggerHealth = currentStaggerHealth;

        _director.stopped += (PlayableDirector pd) => {
            if (currentStaggerHealth <= 0 && !IsStaggered) {
                Stagger();
                currentStaggerHealth = maxStaggerHealth;
            }
        };
        base.Awake();
    }
    public EA GetEnemyAction<EA>(int idx = 0) where EA : EnemyAction
    {
        return _enemyActions[typeof(EA)][idx] as EA;
    }
    public void AddEnemyAction(EnemyAction action)
    {
        if (_enemyActions.ContainsKey(action.GetType()))
        {
            //Debug.LogError($"Enemy Action {action.GetType()} is redundantly referenced, only one should be.");
            //return;
            _enemyActions[action.GetType()].Add(action);
        }
        else 
        {
            _enemyActions[action.GetType()] = new List<EnemyAction> { action };
        }   
    }
    /// <summary>
    /// Select from some attack i to perform, and then provide a direction if the attack has variants based on this
    /// </summary>
    /// <param name="i"></param>
    /// <param name="dir"></param>
    //public void PerformBattleAction(int i)
    //{
    //    if (i >= _enemyActions.Length)
    //    {
    //        Debug.Log("Non Existent index call for batlle action!");
    //        return;
    //    }
    //    _esm.Transition<EnemyStateMachine.Attacking>();
    //    _enemyActions[i].StartAction();
    //    _actionIdx = i;
    //}
    public void StaggerDamage(int staggerDamage)
    {
        if (EnemyData == null)
        {
            Debug.LogError("EnemyData is not assigned.");
            return;
        }
        if (staggerDamage < 0 || currentStaggerHealth <= 0) return;
        currentStaggerHealth -= staggerDamage;
        if (currentStaggerHealth <= 0) {
            if (interruptable) {
                Debug.Log("Regular Stagger");
                Stagger();
                currentStaggerHealth = maxStaggerHealth;
            }
        }
    }
    public Coroutine PlayIntroCutscene()
    {
        return StartCoroutine(Cutscene());
    }
    private IEnumerator Cutscene()
    {
        if (IntroCutscene == null)
        {
            yield return null;
        }
        else
        {
            _director.playableAsset = IntroCutscene;
            _director.timeUpdateMode = DirectorUpdateMode.GameTime;
            _director.Play();
            _director.playableGraph.GetRootPlayable(0).SetSpeed(1);
            yield return new WaitUntil(() => _director.state != PlayState.Playing);
        } 
    }
    #region IAttackReceiver Methods
    public virtual bool ReceiveAttackRequest(IAttackRequester requester)
    {
        if (esm.IsOnState<Dead>() || psm.IsOnState<Distant>()) return false;
        return esm.CurrState.AttackRequestHandler(requester);
    }
    public virtual void CompleteAttackRequest(IAttackRequester requester)
    {
        // Does nothing for now, not waranted an exception!
    }
    #endregion
    #region BattlePawn Overrides
    public override void Damage(int amount)
    {
        amount = esm.CurrState.OnDamage(amount);
        base.Damage(amount);  
    }
    //public override void Lurch(float amount)
    //{
    //    if (IsStaggered) return;
    //    amount = _esm.CurrState.OnLurch(amount);
    //    base.Lurch(amount);
    //}

    protected override void OnStagger()
    {
        if (esm.IsOnState<Dead>()) return;
        base.OnStagger();
        // Staggered Animation (Paper Crumple)
        psm.Transition<Center>();
        esm.Transition<Stagger>();
        _director.Stop();
        OnEnemyStaggerEvent?.Invoke();
        StopAllEnemyActions();
        //_particleSystem?.Play();
    }
    protected override void OnUnstagger()
    {
        if (esm.IsOnState<Dead>()) return;
        base.OnUnstagger();
        // Unstagger Animation transition to idle
        esm.Transition<Idle>();
        //_particleSystem?.Stop();
    }
    protected override void OnDeath()
    {
        base.OnDeath();
        esm.Transition<Dead>();
        StopAllEnemyActions();
        //_particleSystem?.Stop();
    }
    protected void StopAllEnemyActions()
    {
        foreach (List<EnemyAction> list in _enemyActions.Values)
        {
            foreach (EnemyAction action in list)
            {
                action.StopAction();
            }
        }
    }
    // This could get used or not, was intended for random choices :p
    public void OnActionComplete()
    {
    //    OnEnemyActionComplete?.Invoke();
        Debug.LogError("hiiiiiii");
       if (esm.IsOnState<Dead>() || esm.IsOnState<Stagger>()) return;
       esm.Transition<Idle>();
    }
    #endregion
}
