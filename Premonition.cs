using Anvil.API;
using Anvil.API.Events;
using Anvil.Services;
using R2N.Extensions;

namespace R2N.Game.Spells
{
    [ServiceBinding(typeof(Premonition))]
    public class Premonition
    {
        [ScriptHandler("mf_s0_premo")]
        private void EventHandler(CallInfo callInfo)
        {
            SpellEvents.OnSpellCast spell = new SpellEvents.OnSpellCast();

            if (spell.Caster is NwCreature caster)
            {
                caster.RemoveEffectsFromSpell(Spell.Premonition);

                Effect vis = Effect.VisualEffect(VfxType.DurProtPremonition);
                Effect dur = Effect.VisualEffect(VfxType.DurCessatePositive);

                int duration = spell.MetaMagicFeat == MetaMagic.Extend ? caster.CasterLevel * 2 : caster.CasterLevel;
                int totalReduction = caster.CasterLevel * 10;

                Effect reduction = Effect.DamageReduction(20, DamagePower.Plus5, totalReduction);
                reduction = Effect.LinkEffects(reduction, dur);

                if (caster.KnowsFeat(Feat.EpicSpellFocusDivination!))
                {
                    Effect reflex = Effect.SavingThrowIncrease(SavingThrow.Reflex, 4);
                    Effect fort = Effect.SavingThrowIncrease(SavingThrow.Fortitude, 2);
                    Effect will = Effect.SavingThrowIncrease(SavingThrow.Will, 1);
                    reduction = Effect.LinkEffects(reduction, reflex, fort, will);
                }
                else if (caster.KnowsFeat(Feat.GreaterSpellFocusDivination!))
                {
                    Effect reflex = Effect.SavingThrowIncrease(SavingThrow.Reflex, 2);
                    Effect fort = Effect.SavingThrowIncrease(SavingThrow.Fortitude, 1);
                    reduction = Effect.LinkEffects(reduction, reflex, fort);
                }
                else if (caster.KnowsFeat(Feat.SpellFocusDivination!))
                {
                    Effect reflex = Effect.SavingThrowIncrease(SavingThrow.Reflex, 1);
                    reduction = Effect.LinkEffects(reduction, reflex);
                }

                //Fire cast spell at event for the specified target
                caster.SignalSpellCastAt(caster, spell.Spell.SpellType, false);

                caster.ApplyEffect(EffectDuration.Temporary, reduction, NwTimeSpan.FromHours(duration));
                caster.ApplyEffect(EffectDuration.Temporary, vis, NwTimeSpan.FromRounds(1));
            }
        }
    }
}

