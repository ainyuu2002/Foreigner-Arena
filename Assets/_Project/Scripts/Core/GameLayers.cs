using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Tra cứu layer THEO TÊN một lần (KHÔNG hardcode index số — §1.1/§3.5 CLAUDE.md).
    /// Việc LỌC va chạm hitbox↔hurtbox do Physics2D Collision Matrix lo (Project Settings),
    /// KHÔNG phải các câu "if (layer == ...)". Lớp này chỉ cấp index + mask cho các query
    /// như Collider2D.IsTouchingLayers / Physics2D.OverlapBox khi cần ground/wall check.
    ///
    /// Tên phải khớp đúng các user layer khai trong Project Settings → Tags and Layers (§2).
    /// </summary>
    public static class GameLayers
    {
        public const string GroundName = "Ground";
        public const string OneWayPlatformName = "OneWayPlatform";
        public const string WallName = "Wall";
        public const string Player1Name = "Player1";
        public const string Player2Name = "Player2";
        public const string HitboxName = "Hitbox";
        public const string HurtboxName = "Hurtbox";
        public const string DeathZoneName = "DeathZone";
        public const string ItemName = "Item";
        public const string ProjectileP1Name = "ProjectileP1";
        public const string ProjectileP2Name = "ProjectileP2";

        public static readonly int Ground = LayerMask.NameToLayer(GroundName);
        public static readonly int OneWayPlatform = LayerMask.NameToLayer(OneWayPlatformName);
        public static readonly int Wall = LayerMask.NameToLayer(WallName);
        public static readonly int Player1 = LayerMask.NameToLayer(Player1Name);
        public static readonly int Player2 = LayerMask.NameToLayer(Player2Name);
        public static readonly int Hitbox = LayerMask.NameToLayer(HitboxName);
        public static readonly int Hurtbox = LayerMask.NameToLayer(HurtboxName);
        public static readonly int DeathZone = LayerMask.NameToLayer(DeathZoneName);
        public static readonly int Item = LayerMask.NameToLayer(ItemName);
        public static readonly int ProjectileP1 = LayerMask.NameToLayer(ProjectileP1Name);
        public static readonly int ProjectileP2 = LayerMask.NameToLayer(ProjectileP2Name);

        public static int GroundMask => 1 << Ground;
        public static int OneWayPlatformMask => 1 << OneWayPlatform;
        public static int WallMask => 1 << Wall;
        public static int Player1Mask => 1 << Player1;
        public static int Player2Mask => 1 << Player2;
        public static int HitboxMask => 1 << Hitbox;
        public static int HurtboxMask => 1 << Hurtbox;
        public static int DeathZoneMask => 1 << DeathZone;
        public static int ItemMask => 1 << Item;
        public static int ProjectileP1Mask => 1 << ProjectileP1;
        public static int ProjectileP2Mask => 1 << ProjectileP2;

        /// <summary>Mask gộp mọi bề mặt đứng được (sàn + platform 1 chiều) — tiện cho GroundCheck (P1).</summary>
        public static int StandableMask => GroundMask | OneWayPlatformMask;
    }
}
