using System;
using System.Threading.Tasks;
using Anvil.API;
using NWN.Core;

namespace R2N.Extensions
{
    /// <summary>
    /// Extensions to the NwGameObject class
    /// </summary>
    public static class NwGameObjectExtensions
    {
        /// <summary>
        /// Determines spell countering and failing along with visual effects by a creature's saving throws.
        /// An appropriate save attempt is made against the supplied DC according using the save specified by savingThrow.
        // If successful, a visual effect of the specified save being executed is shown
        /// (for example, SAVING_THROW_REFLEX would show the creature moving). If the save fails, or no save is allowed,
        /// then the effects of the spell are applied (this includes any instant death animations as indicated by the type of spell).
        /// If the creature is immune to the spell being cast and the save is failed, then no effect is applied an the spell resistance visual cue is played.
        /// </summary>
        /// <param name="target">Target of a spell</param>
        /// <param name="savingThrow">SavingThrow enum</param>
        /// <param name="dc">Difficulty challenge rating of the spell's save</param>
        /// <param name="saveType">SavingThrowType enum</param>
        /// <param name="saveVersus">Creature or object to save against</param>
        /// <param name="delay">Delay until save is made</param>
        /// <returns>Returns true only if the saving throw was made, returns false otherwise
        ///(even if immunity to nSaveType (such as posion immunity to SAVING_THROW_TYPE_POISON)
        /// prevents the spell from affecting the creature).</returns>
        public static async Task<bool> MySavingThrow(this NwGameObject target, SavingThrow savingThrow, int dc, SavingThrowType saveType = SavingThrowType.All, NwGameObject? saveVersus = null, NwSpell? spell = null, float delay = 0.0f)
        {
            // -------------------------------------------------------------------------
            // GZ: sanity checks to prevent wrapping around
            // -------------------------------------------------------------------------
            if (dc < 1)
            {
                dc = 1;
            }
            else if (dc > 255)
            {
                dc = 255;
            }

            saveVersus ??= target;

            Effect eVis = Effect.VisualEffect(VfxType.ImpFortitudeSavingThrowUse);
            SavingThrowResult result = target.RollSavingThrow(savingThrow, dc, saveType, saveVersus);

            if (result == SavingThrowResult.Success &&  savingThrow == SavingThrow.Fortitude)
            {
                eVis = Effect.VisualEffect(VfxType.ImpFortitudeSavingThrowUse);
            }
            else if (result == SavingThrowResult.Success && savingThrow == SavingThrow.Reflex)
            {
                eVis = Effect.VisualEffect(VfxType.ImpReflexSaveThrowUse);
            }
            else if (result == SavingThrowResult.Success && savingThrow == SavingThrow.Will)
            {
                eVis = Effect.VisualEffect(VfxType.ImpWillSavingThrowUse);
            }  

            /*
                return 0 = FAILED SAVE
                return 1 = SAVE SUCCESSFUL
                return 2 = IMMUNE TO WHAT WAS BEING SAVED AGAINST
            */
            if (result == SavingThrowResult.Failure
                &&
                (saveType == SavingThrowType.Death
                || (spell is not null &&
                        (spell.SpellType == Spell.Weird
                        || spell.SpellType == Spell.FingerOfDeath
                        || spell.SpellType == Spell.HorridWilting)))
                )
            {
                eVis = Effect.VisualEffect(VfxType.ImpDeath);
                await NwTask.Delay(TimeSpan.FromSeconds(delay));
                target.ApplyEffect(EffectDuration.Instant, eVis);
            }

            if (result == SavingThrowResult.Success || result == SavingThrowResult.Immune)
            {
                if (result == SavingThrowResult.Immune)
                {
                    eVis = Effect.VisualEffect(VfxType.ImpMagicResistanceUse);
                    /*
                    If the spell is save immune then the link must be applied in order to get the true immunity
                    to be resisted.  That is the reason for returing false and not true.  True blocks the
                    application of effects.
                    */
                    result = SavingThrowResult.Failure;
                }
                await NwTask.Delay(TimeSpan.FromSeconds(delay));
                target.ApplyEffect(EffectDuration.Instant, eVis);
            }
            return Convert.ToBoolean(result);
        }

        /// <summary>
        /// Removes spell effects from a creature
        /// </summary>
        /// <param name="caster">Spell caster.</param>
        /// <param name="target">Spell target.</param>
        /// <param name="spell">Spell casted.</param>
        /// <param name="harmful">True if this is a harmful spell.</param>
        public static void SignalSpellCastAt(this NwGameObject caster, NwGameObject target, Spell spell, bool harmful = true)
        {
            NWScript.SignalEvent(target, NWScript.EventSpellCastAt(caster, (int)spell, harmful.ToInt()));
        }

        /// <summary>
        /// Removes spell effects from a creature
        /// </summary>
        /// <param name="target">Damage target.</param>
        /// <param name="damage">Damage amount.</param>
        /// <param name="dc">How difficult is to avoid damage.</param>
        /// <param name="saveType">Saving throw type.</param>
        /// <param name="saveVs">Damage source.</param>
        public static int GetReflexAdjustedDamage(this NwGameObject target, int damage, int dc, SavingThrowType saveType, NwGameObject? saveVs = null)
        {
            if (target is not NwCreature creature)
            {
                return damage;
            }

            if (creature.RollSavingThrow(SavingThrow.Reflex, dc, saveType, saveVs) == SavingThrowResult.Failure)
            {
                if (creature.KnowsFeat(Feat.ImprovedEvasion))
                {
                    damage /= 2;
                }
            }
            else if (creature.KnowsFeat(Feat.Evasion) || creature.KnowsFeat(Feat.ImprovedEvasion))
            {
                damage = 0;
            }
            else
            {
                damage /= 2;
            }

            return damage;
        }

        /// <summary>
        /// Determines and plays animation for resisting a spell when applicable
        /// </summary>
        /// <param name="caster">Caster of a spell</param>
        /// <param name="target">Target of a spell</param>
        /// <param name="delay">Delay until visual effect is played</param>
        /// <returns>if caster or target is an invalid object: FALSE,
        /// if spell cast is not a player spell: -1,
        /// if spell resisted: 1,
        /// if spell resisted via magic immunity: 2,
        /// if spell resisted via spell absorption: 3</returns>
        public static async Task<ResistSpellResult> MyResistSpell(this NwGameObject target, NwCreature caster, float delay = 0.01f)
        {
            // no SR check for doors and placeables
            if (target is not NwCreature)
            {
                return 0;
            }

            ResistSpellResult resist = caster.CheckResistSpell(target);
            Effect sr = Effect.VisualEffect(VfxType.ImpMagicResistanceUse);
            Effect globe = Effect.VisualEffect(VfxType.ImpGlobeUse);
            Effect mantle = Effect.VisualEffect(VfxType.ImpSpellMantleUse);

            if (resist == ResistSpellResult.Resisted) //Spell Resistance
            {
                await NwTask.Delay(TimeSpan.FromSeconds(delay));
                target.ApplyEffect(EffectDuration.Instant, sr);
            }
            else if (resist == ResistSpellResult.ResistedMagicImmune) //Globe
            {
                await NwTask.Delay(TimeSpan.FromSeconds(delay));
                target.ApplyEffect(EffectDuration.Instant, globe);
            }
            else if (resist == ResistSpellResult.ResistedSpellAbsorbed) //Spell Mantle
            {
                await NwTask.Delay(TimeSpan.FromSeconds(delay));
                target.ApplyEffect(EffectDuration.Instant, mantle);
            }

            return resist;
        }
    }
}
