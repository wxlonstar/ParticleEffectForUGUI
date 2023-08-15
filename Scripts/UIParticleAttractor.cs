﻿using UnityEngine;
using Coffee.UIParticleExtensions;
using UnityEngine.Events;
using System;

namespace Coffee.UIExtensions
{
    [ExecuteAlways]
    public class UIParticleAttractor : MonoBehaviour
    {
        public enum Movement
        {
            Linear,
            Smooth,
            Sphere,
        }

        [SerializeField]
        private ParticleSystem m_ParticleSystem;

        [Range(0.1f, 10f)]
        [SerializeField]
        private float m_DestinationRadius = 1;

        [Range(0f, 0.95f)]
        [SerializeField]
        private float m_DelayRate = 0;

        [Range(0.001f, 100f)]
        [SerializeField]
        private float m_MaxSpeed = 1;

        [SerializeField]
        private Movement m_Movement;

        [SerializeField]
        private UnityEvent m_OnAttracted;

        public float destinationRadius
        {
            get { return m_DestinationRadius; }
            set { m_DestinationRadius = Mathf.Clamp(value, 0.1f, 10f); }
        }

        public float delay
        {
            get
            {
                return m_DelayRate;
            }
            set
            {
                m_DelayRate = value;
            }
        }

        public float maxSpeed
        {
            get
            {
                return m_MaxSpeed;
            }
            set
            {
                m_MaxSpeed = value;
            }
        }

        public Movement movement
        {
            get
            {
                return m_Movement;
            }
            set
            {
                m_Movement = value;
            }
        }

        public UnityEvent onAttracted
        {
            get { return m_OnAttracted; }
            set { m_OnAttracted = value; }
        }

        public ParticleSystem particleSystem
        {
            get
            {
                return m_ParticleSystem;
            }
            set
            {
                m_ParticleSystem = value;
                if (!ApplyParticleSystem()) return;
                enabled = true;
            }
        }

        private UIParticle _uiParticle;

        private void OnEnable()
        {
            if (!ApplyParticleSystem()) return;
            UIParticleUpdater.Register(this);
        }

        private void OnDisable()
        {
            _uiParticle = null;
            UIParticleUpdater.Unregister(this);
        }

        internal void Attract()
        {
            if (m_ParticleSystem == null) return;

            var count = m_ParticleSystem.particleCount;
            if (count == 0) return;

            var particles = ParticleSystemExtensions.GetParticleArray(count);
            m_ParticleSystem.GetParticles(particles, count);

            var dstPos = GetDestinationPosition();
            for (var i = 0; i < count; i++)
            {
                // Attracted
                var p = particles[i];
                if (0f < p.remainingLifetime && Vector3.Distance(p.position, dstPos) < m_DestinationRadius)
                {
                    p.remainingLifetime = 0f;
                    particles[i] = p;

                    if (m_OnAttracted != null)
                    {
                        try
                        {
                            m_OnAttracted.Invoke();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                    continue;
                }

                // Calc attracting time
                var delayTime = p.startLifetime * m_DelayRate;
                var duration = p.startLifetime - delayTime;
                var time = Mathf.Max(0, p.startLifetime - p.remainingLifetime - delayTime);

                // Delay
                if (time <= 0) continue;

                // Attract
                p.position = GetAttractedPosition(p.position, dstPos, duration, time);
                p.velocity *= 0.5f;
                particles[i] = p;
            }

            m_ParticleSystem.SetParticles(particles, count);
        }

        private Vector3 GetDestinationPosition()
        {
            var isUI = _uiParticle && _uiParticle.enabled;
            var psPos = m_ParticleSystem.transform.position;
            var attractorPos = transform.position;
            var dstPos = attractorPos;
            var isLocalSpace = m_ParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Local;

            if (isLocalSpace)
            {
                dstPos = m_ParticleSystem.transform.InverseTransformPoint(dstPos);
            }

            if (isUI)
            {
                dstPos = dstPos.GetScaled(_uiParticle.transform.localScale, _uiParticle.scale3D.Inverse());

                // Relative mode
                if (!_uiParticle.absoluteMode)
                {
                    var diff = _uiParticle.transform.position - psPos;
                    diff.Scale(_uiParticle.scale3D - _uiParticle.transform.localScale);
                    diff.Scale(_uiParticle.scale3D.Inverse());
                    dstPos += diff;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying && !isLocalSpace)
                {
                    dstPos += psPos - psPos.GetScaled(_uiParticle.transform.localScale, _uiParticle.scale3D.Inverse());
                }
#endif
            }
            return dstPos;
        }

        private Vector3 GetAttractedPosition(Vector3 current, Vector3 target, float duration, float time)
        {
            var speed = m_MaxSpeed;
            switch (m_Movement)
            {
                case Movement.Linear:
                    speed /= duration;
                    break;
                case Movement.Smooth:
                    target = Vector3.Lerp(current, target, time / duration);
                    break;
                case Movement.Sphere:
                    target = Vector3.Slerp(current, target, time / duration);
                    break;
            }

            return Vector3.MoveTowards(current, target, speed);
        }

        private bool ApplyParticleSystem()
        {
            if (m_ParticleSystem == null)
            {
                Debug.LogError("No particle system attached to particle attractor script", this);
                enabled = false;
                return false;
            }

            _uiParticle = m_ParticleSystem.GetComponentInParent<UIParticle>();
            if (_uiParticle && !_uiParticle.particles.Contains(m_ParticleSystem))
            {
                _uiParticle = null;
            }

            return true;
        }
    }
}