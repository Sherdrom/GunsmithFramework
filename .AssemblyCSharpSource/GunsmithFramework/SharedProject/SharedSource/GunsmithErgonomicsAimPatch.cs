using Barotrauma.Items.Components;
using FarseerPhysics;

namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithErgonomicsAimPatch
    {
        private const float RaiseTravelRadians = MathHelper.Pi;

        private sealed class AimRaiseRuntime
        {
            public Item? Item;
            public bool WasAiming;
            public bool IsRaising;
            public float RaiseProgress;
            public float RaiseElapsed;
            public float RaiseDuration;
            public float StartAngle;
        }

        private static readonly Dictionary<Character, AimRaiseRuntime> runtimes = new();
        private static readonly AccessTools.FieldRef<Pickable, Character> pickerRef = AccessTools.FieldRefAccess<Pickable, Character>("picker");

        private static MethodBase TargetMethod()
        {
            MethodInfo? method = AccessTools.GetDeclaredMethods(typeof(Holdable))
                .FirstOrDefault(candidate =>
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    return candidate.Name == nameof(Holdable.Update) &&
                           parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(float);
                });

            return method ?? throw new MissingMethodException(typeof(Holdable).FullName, nameof(Holdable.Update));
        }

        private static bool Prefix(Holdable __instance, float deltaTime)
        {
            Item item = __instance.Item;
            if (item.body == null || !item.body.Enabled)
            {
                return true;
            }

            Character? picker = pickerRef(__instance);
            if (!ShouldHandlePicker(picker, item))
            {
                ClearIfOwned(picker, item);
                return true;
            }

            if (item.GetComponent<RangedWeapon>() == null || !GunsmithRuntimeStates.TryGet(item, out GunsmithRuntimeState state))
            {
                ClearIfOwned(picker, item);
                return true;
            }

            AimRaiseRuntime runtime = RuntimeFor(picker);
            if (!ReferenceEquals(runtime.Item, item))
            {
                runtime.Item = item;
                runtime.WasAiming = false;
                runtime.IsRaising = false;
                runtime.RaiseProgress = 1.0f;
                runtime.RaiseElapsed = 0.0f;
                runtime.RaiseDuration = 0.0f;
                runtime.StartAngle = 0.0f;
            }

            if (!IsAimInputDown(__instance, picker))
            {
                runtime.WasAiming = false;
                runtime.IsRaising = false;
                runtime.RaiseProgress = 1.0f;
                runtime.RaiseElapsed = 0.0f;
                return true;
            }

            if (!runtime.WasAiming)
            {
                runtime.RaiseProgress = 0.0f;
                runtime.RaiseElapsed = 0.0f;
                runtime.RaiseDuration = RaiseDurationSeconds(state);
                runtime.StartAngle = CurrentTransformedRotation(item);
                runtime.IsRaising = true;
            }
            runtime.WasAiming = true;

            if (!runtime.IsRaising)
            {
                return true;
            }

            ApplyManualHold(__instance, picker, item, runtime, deltaTime);
            runtime.RaiseElapsed += Math.Max(deltaTime, (float)Timing.Step);
            runtime.RaiseProgress = runtime.RaiseDuration <= 0.0f ? 1.0f : MathHelper.Clamp(runtime.RaiseElapsed / runtime.RaiseDuration, 0.0f, 1.0f);
            runtime.IsRaising = runtime.RaiseProgress < 1.0f;
            if (!runtime.IsRaising)
            {
                runtime.RaiseProgress = 1.0f;
            }
            return false;
        }

        internal static bool ShouldSuppressUse(Character? picker, Item item)
        {
            if (item == null || item.body == null || !item.body.Enabled || !ShouldHandlePicker(picker, item))
            {
                return false;
            }

            Holdable? holdable = item.GetComponent<Holdable>();
            if (holdable == null || !GunsmithRuntimeStates.TryGet(item, out _) || !IsAimInputDown(holdable, picker!))
            {
                return false;
            }

            if (runtimes.TryGetValue(picker!, out AimRaiseRuntime? runtime) && ReferenceEquals(runtime.Item, item))
            {
                return !runtime.WasAiming || runtime.IsRaising;
            }

            return true;
        }

        private static bool ShouldHandlePicker(Character? picker, Item item)
        {
            if (picker == null ||
                picker.Removed ||
                picker.HeldItems == null ||
                !picker.HasEquippedItem(item) ||
                !picker.HeldItems.Contains(item))
            {
                return false;
            }

#if CLIENT
            if (picker != Character.Controlled)
            {
                return false;
            }
#endif

            return true;
        }

        private static bool IsAimInputDown(Holdable holdable, Character picker)
            => picker.IsKeyDown(InputType.Aim) && picker.CanAim && holdable.AimPos != Vector2.Zero;

        private static void ApplyManualHold(Holdable holdable, Character picker, Item item, AimRaiseRuntime runtime, float deltaTime)
        {
            holdable.UpdateSwingPos(deltaTime, out Vector2 swingPos);
            if (item.body.Dir != picker.AnimController.Dir)
            {
                item.FlipX(relativeToSub: false);
            }

            item.Submarine = picker.Submarine;

            Vector2[] scaledHandlePos =
            {
                ConvertUnits.ToSimUnits(holdable.Handle1) * item.Scale,
                ConvertUnits.ToSimUnits(holdable.Handle2) * item.Scale
            };

            Vector2 cursorDirection = picker.CursorPosition - picker.AimRefPosition;
            float cursorDistance = MathHelper.Clamp(cursorDirection.Length(), 100.0f, 2000.0f);
            float targetAngle = cursorDirection.LengthSquared() < 0.001f ? runtime.StartAngle : MathF.Atan2(cursorDirection.Y, cursorDirection.X);
            float virtualAngle = LerpAngle(runtime.StartAngle, targetAngle, SmoothStep(runtime.RaiseProgress));
            Vector2 virtualDirection = new(MathF.Cos(virtualAngle), MathF.Sin(virtualAngle));
            Vector2 targetSimPos = picker.AnimController.AimSourceSimPos + ConvertUnits.ToSimUnits(virtualDirection * cursorDistance);
            Vector2 itemPos = Vector2.Lerp(
                ConvertUnits.ToSimUnits(holdable.HoldPos),
                ConvertUnits.ToSimUnits(holdable.AimPos),
                SmoothStep(runtime.RaiseProgress)) + swingPos;

            picker.AnimController.HoldItem(
                deltaTime,
                item,
                scaledHandlePos,
                itemPos: itemPos,
                aim: true,
                MathHelper.ToRadians(holdable.HoldAngle),
                MathHelper.ToRadians(holdable.AimAngle),
                targetPos: targetSimPos);
        }

        private static AimRaiseRuntime RuntimeFor(Character character)
        {
            if (!runtimes.TryGetValue(character, out AimRaiseRuntime? runtime))
            {
                runtime = new AimRaiseRuntime();
                runtimes[character] = runtime;
            }
            return runtime;
        }

        private static void ClearIfOwned(Character? character, Item item)
        {
            if (character == null)
            {
                return;
            }

            if (runtimes.TryGetValue(character, out AimRaiseRuntime? runtime) && ReferenceEquals(runtime.Item, item))
            {
                runtimes.Remove(character);
            }
        }

        private static float AimFollowRadiansPerSecond(GunsmithRuntimeState state)
        {
            float degrees = 270.0f + state.Stats.Ergonomics * 3.15f;
            degrees = MathHelper.Clamp(degrees, 180.0f, 900.0f);
            return MathHelper.ToRadians(degrees);
        }

        private static float RaiseDurationSeconds(GunsmithRuntimeState state)
            => RaiseTravelRadians / AimFollowRadiansPerSecond(state);

        private static float CurrentTransformedRotation(Item item)
        {
            if (item.body == null)
            {
                return item.RotationRad;
            }

            float rotation = item.body.Rotation;
            if (item.body.Dir < 0.0f)
            {
                rotation += MathHelper.Pi;
            }
            return WrapAngle(rotation);
        }

        private static float LerpAngle(float start, float end, float amount)
            => start + WrapAngle(end - start) * MathHelper.Clamp(amount, 0.0f, 1.0f);

        private static float WrapAngle(float angle)
        {
            while (angle > MathF.PI)
            {
                angle -= MathF.PI * 2.0f;
            }
            while (angle < -MathF.PI)
            {
                angle += MathF.PI * 2.0f;
            }
            return angle;
        }

        private static float SmoothStep(float value)
            => value * value * (3.0f - 2.0f * value);
    }

    [HarmonyPatch(typeof(RangedWeapon), nameof(RangedWeapon.Use), new[] { typeof(float), typeof(Character) })]
    public static class GunsmithErgonomicsRangedWeaponUsePatch
    {
        private static bool Prefix(RangedWeapon __instance, Character? character, ref bool __result)
        {
            if (!GunsmithErgonomicsAimPatch.ShouldSuppressUse(character, __instance.Item))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
