#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    [TestFixture]
    public class ScoringTests
    {
        private const double Eps = 1e-9;

        // isDrifting: needs both large lateral slip and sufficient speed
        [Test]
        public void IsDrifting_NeedsBothSlipAndSpeed()
        {
            Assert.That(Scoring.IsDrifting(70, 200), Is.True);
            Assert.That(Scoring.IsDrifting(0, 200), Is.False);
            Assert.That(Scoring.IsDrifting(70, 80), Is.False);
            Assert.That(Scoring.IsDrifting(60, 200), Is.False);
            Assert.That(Scoring.IsDrifting(-70, 200), Is.True);
        }

        // driftQuality: normalised slip×speed with ceiling
        [Test]
        public void DriftQuality_NormalisedWithCeiling()
        {
            Assert.That(Scoring.DriftQuality(160, 260), Is.EqualTo(1).Within(Eps));
            Assert.That(Scoring.DriftQuality(0, 260), Is.EqualTo(0).Within(Eps));
            Assert.That(Scoring.DriftQuality(320, 520), Is.EqualTo(Scoring.QualityMax));
        }

        // comboMult: 1 + multBuild, capped at MULT_MAX
        [Test]
        public void ComboMult_CappedAtMax()
        {
            Assert.That(Scoring.ComboMult(0), Is.EqualTo(1).Within(Eps));
            Assert.That(Scoring.ComboMult(3), Is.EqualTo(4).Within(Eps));
            Assert.That(Scoring.ComboMult(7), Is.EqualTo(Scoring.MultMax));
            Assert.That(Scoring.ComboMult(50), Is.EqualTo(Scoring.MultMax));
        }

        // comboGain: slip × speed × dt × mult × COMBO_RATE
        [Test]
        public void ComboGain_Formula()
        {
            Assert.That(Scoring.ComboGain(160, 260, 1, 1), Is.EqualTo(160 * 260 * Scoring.ComboRate).Within(Eps));
            Assert.That(Scoring.ComboGain(100, 100, 0.5, 2), Is.EqualTo(100 * 100 * 0.5 * Scoring.ComboRate * 2).Within(Eps));
            Assert.That(Scoring.ComboGain(0, 260, 1, 5), Is.EqualTo(0).Within(Eps));
        }

        // slipSign: +1 / -1 / 0 by threshold
        [Test]
        public void SlipSign_ByThreshold()
        {
            Assert.That(Scoring.SlipSign(60), Is.EqualTo(1));
            Assert.That(Scoring.SlipSign(-60), Is.EqualTo(-1));
            Assert.That(Scoring.SlipSign(10), Is.EqualTo(0));
            Assert.That(Scoring.SlipSign(50), Is.EqualTo(0));
            Assert.That(Scoring.SlipSign(-50), Is.EqualTo(0));
        }

        // pointsPerSecond: points / time, protected against division by zero
        [Test]
        public void PointsPerSecond_ProtectedAgainstZeroTime()
        {
            Assert.That(Scoring.PointsPerSecond(45000, 36), Is.EqualTo(1250).Within(Eps));
            Assert.That(Scoring.PointsPerSecond(0, 36), Is.EqualTo(0).Within(Eps));
            Assert.That(Scoring.PointsPerSecond(45000, 0), Is.EqualTo(0).Within(Eps));
        }

        // isNewPpsRecord: no prior record (null/undefined) always counts as new
        [Test]
        public void IsNewPpsRecord_NoPriorRecordIsNew()
        {
            Assert.That(Scoring.IsNewPpsRecord(null, 1000), Is.True);
        }

        // isNewPpsRecord: beats the stored best only when strictly higher
        [Test]
        public void IsNewPpsRecord_OnlyWhenStrictlyHigher()
        {
            Assert.That(Scoring.IsNewPpsRecord(1000, 1200), Is.True);
            Assert.That(Scoring.IsNewPpsRecord(1000, 800), Is.False);
            Assert.That(Scoring.IsNewPpsRecord(1000, 1000), Is.False);
        }

        [Test]
        public void ConstantsMatchExportedJson()
        {
            var j = (Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(
                TestPaths.Locate("unity/Assets/Content/_json/scoring.json")))!;
            void Eq(string key, double actual) =>
                Assert.That(actual, Is.EqualTo((double)j[key]!), key);
            Eq("DRIFT_MIN_SLIP", Scoring.DriftMinSlip);
            Eq("DRIFT_MIN_SPEED", Scoring.DriftMinSpeed);
            Eq("QUALITY_SLIP_REF", Scoring.QualitySlipRef);
            Eq("QUALITY_SPEED_REF", Scoring.QualitySpeedRef);
            Eq("QUALITY_MAX", Scoring.QualityMax);
            Eq("MULT_GAIN_PER_S", Scoring.MultGainPerS);
            Eq("MULT_TRANSITION_BONUS", Scoring.MultTransitionBonus);
            Eq("MULT_NEARMISS_BONUS", Scoring.MultNearmissBonus);
            Eq("MULT_MAX", Scoring.MultMax);
            Eq("SLIP_SIGN_THRESHOLD", Scoring.SlipSignThreshold);
            Eq("COMBO_RATE", Scoring.ComboRate);
        }
    }
}
