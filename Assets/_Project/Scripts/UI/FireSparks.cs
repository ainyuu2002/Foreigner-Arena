using UnityEngine;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX tầng 2) TÀN LỬA bay lên từ chân cột lửa — port từ demo HTML canvas (class Spark/Ember),
    /// tinh chỉnh cho project: namespace FA.UI, material URP-compatible qua <see cref="MenuFXAssets"/>
    /// (shader built-in ra MAGENTA trong URP build), texture chấm tròn mềm sinh bằng code (không cần asset).
    /// Script tự cấu hình toàn bộ ParticleSystem lúc Start — đặt 2 instance ở chân 2 cột lửa của BG art.
    /// Mục đích mỹ thuật: lửa VẼ trong ảnh đứng im — tàn lửa bay thật đánh lừa mắt rằng cả đám lửa đang cháy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FireSparks : MonoBehaviour
    {
        [Header("Vùng phát")]
        [Tooltip("Bề rộng chân lửa (world units).")]
        public float emitWidth = 3f;

        [Header("Mật độ & chuyển động")]
        public float emissionRate = 35f;
        public float speedMin = 1.2f;
        public float speedMax = 3.5f;
        public float lifeMin = 1.5f;
        public float lifeMax = 3.5f;
        public float sizeMin = 0.03f;
        public float sizeMax = 0.12f;
        [Tooltip("Lắc lư ngang (thay Math.sin wobble của bản JS).")]
        public float sidewaysDrift = 0.6f;

        [Header("Màu (cam lửa — khớp BG art)")]
        public Color hotColor = new Color(1f, 0.76f, 0.30f);
        public Color midColor = new Color(1f, 0.48f, 0f);
        public Color coolColor = new Color(1f, 0.32f, 0f, 0f);

        [Header("Render")]
        public string sortingLayerName = "Default";
        [Tooltip("Giữa BG canvas (250) và menu content (300) — cùng tầng với sét (255).")]
        public int sortingOrder = 256;

        [Tooltip("Giới hạn hạt mỗi hệ (2 hệ trái/phải → tổng ×2). Mobile giảm xuống.")]
        public int maxParticles = 200;

        private void Start()
        {
            var ps = GetComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(hotColor, midColor);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            var emission = ps.emission;
            emission.rateOverTime = emissionRate;

            // Phát từ dải ngang ở chân (giống spawn dọc mép dưới trong JS).
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(emitWidth, 0.1f, 0.1f);

            // CẢ 3 TRỤC phải cùng mode MinMaxCurve (Unity bắt buộc) — z không dùng vẫn phải set TwoConstants,
            // kẻo lỗi "Particle Velocity curves must all be in the same mode".
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-sidewaysDrift, sidewaysDrift);
            vel.y = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Lắc lư như gió.
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.6f;
            noise.damping = true;

            // Nhỏ dần theo vòng đời.
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.7f, 0.6f),
                    new Keyframe(1f, 0f)));

            // Nóng → nguội → tắt.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(hotColor, 0f),
                    new GradientColorKey(midColor, 0.4f),
                    new GradientColorKey(coolColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            // Material additive URP + chấm tròn mềm (thay shadowBlur của canvas JS).
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = MenuFXAssets.AdditiveMaterial(withSoftCircle: true);
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            ps.Play();
        }
    }
}
