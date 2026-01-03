using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace AngryKoala.UI
{
    public class Button : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Transform _buttonVisual;

        [SerializeField] private bool _animateOnClick = true;

        [SerializeField] private Vector3 _pressedMoveBy;
        [SerializeField] private Vector3 _pressedRotateBy;
        [SerializeField] private Vector3 _pressedScaleBy;

        [SerializeField] private float _pressDuration = 0.2f;
        [SerializeField] private float _releaseDuration = 0.2f;

        [SerializeField] private Ease _pressEase = Ease.OutQuad;
        [SerializeField] private Ease _releaseEase = Ease.OutQuad;

        [SerializeField] private bool _allowMultipleClicks = true;
        [SerializeField] private float _disableDuration = 0.2f;

        [SerializeField] private bool _disableAfterClick;

        private bool _isClickable = true;
        private bool _isPointerDown = false;
        private bool _isPointerInside = false;

        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;

        private Tween _positionTween;
        private Tween _rotationTween;
        private Tween _scaleTween;

        private Tween _clickCooldownTween;

        public UnityEvent OnPointerDownEvent;
        public UnityEvent OnPointerUpEvent;
        public UnityEvent OnClickEvent;

        private void Awake()
        {
            SetInitialTransformValues();
        }

        private void OnEnable()
        {
            ResetTransformValues();
            CancelClickCooldown();
        }

        private void OnDisable()
        {
            CancelClickCooldown();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_isClickable)
            {
                return;
            }

            _isPointerDown = true;
            _isPointerInside = true;

            if (_animateOnClick)
            {
                AnimateToPressedPose();
            }

            OnPointerDownEvent?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isClickable)
            {
                return;
            }

            bool shouldClick = _isPointerDown && _isPointerInside;

            _isPointerDown = false;

            if (_animateOnClick)
            {
                AnimateToReleasedPose();
            }
            else
            {
                ResetTransformValues();
            }

            OnPointerUpEvent?.Invoke();

            if (shouldClick)
            {
                OnClickEvent?.Invoke();

                if (_disableAfterClick)
                {
                    DisableButton();
                }
                else if (!_allowMultipleClicks)
                {
                    StartClickCooldown();
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;

            if (_isPointerDown && _isClickable && _animateOnClick)
            {
                AnimateToPressedPose();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;

            if (_isPointerDown && _animateOnClick)
            {
                AnimateToReleasedPose();
            }
        }

        public void SetInitialTransformValues()
        {
            _initialLocalPosition = _buttonVisual.localPosition;
            _initialLocalRotation = _buttonVisual.localRotation;
            _initialLocalScale = _buttonVisual.localScale;
        }

        public void ResetTransformValues()
        {
            _buttonVisual.localPosition = _initialLocalPosition;
            _buttonVisual.localRotation = _initialLocalRotation;
            _buttonVisual.localScale = _initialLocalScale;
        }

        public void EnableButton()
        {
            _isClickable = true;
        }

        public void DisableButton()
        {
            _isClickable = false;
        }

        private void StartClickCooldown()
        {
            if (_disableDuration <= 0f)
            {
                return;
            }

            CancelClickCooldown();
            _isClickable = false;

            _clickCooldownTween = DOVirtual.DelayedCall(_disableDuration, () =>
                {
                    if (!_disableAfterClick)
                    {
                        _isClickable = true;
                    }
                })
                .SetId("UI")
                .SetLink(gameObject);
        }

        private void CancelClickCooldown()
        {
            if (_clickCooldownTween != null && _clickCooldownTween.IsActive())
            {
                _clickCooldownTween.Kill();
            }

            _clickCooldownTween = null;
        }

        private void AnimateToPressedPose()
        {
            KillTweens();

            Vector3 pressedLocalPosition = _initialLocalPosition + _pressedMoveBy;
            Vector3 pressedRotateBy = (_initialLocalRotation * Quaternion.Euler(_pressedRotateBy)).eulerAngles;
            Vector3 pressedLocalScale = _initialLocalScale + _pressedScaleBy;

            _positionTween = _buttonVisual.DOLocalMove(pressedLocalPosition, _pressDuration)
                .SetEase(_pressEase)
                .SetId("UI")
                .SetLink(gameObject);

            _rotationTween = _buttonVisual.DORotate(pressedRotateBy, _pressDuration)
                .SetEase(_pressEase)
                .SetId("UI")
                .SetLink(gameObject);

            _scaleTween = _buttonVisual.DOScale(pressedLocalScale, _pressDuration)
                .SetEase(_pressEase)
                .SetId("UI")
                .SetLink(gameObject);
        }

        private void AnimateToReleasedPose()
        {
            KillTweens();

            _positionTween = _buttonVisual.DOLocalMove(_initialLocalPosition, _releaseDuration)
                .SetEase(_releaseEase)
                .SetId("UI")
                .SetLink(gameObject);

            _rotationTween = _buttonVisual.DORotate(_initialLocalRotation.eulerAngles, _releaseDuration)
                .SetEase(_releaseEase)
                .SetId("UI")
                .SetLink(gameObject);

            _scaleTween = _buttonVisual.DOScale(_initialLocalScale, _releaseDuration)
                .SetEase(_releaseEase)
                .SetId("UI")
                .SetLink(gameObject);
        }

        private void KillTweens()
        {
            if (_positionTween != null && _positionTween.IsActive())
            {
                _positionTween.Kill();
            }

            if (_rotationTween != null && _rotationTween.IsActive())
            {
                _rotationTween.Kill();
            }

            if (_scaleTween != null && _scaleTween.IsActive())
            {
                _scaleTween.Kill();
            }

            DOTween.Kill(_buttonVisual);
        }
    }
}