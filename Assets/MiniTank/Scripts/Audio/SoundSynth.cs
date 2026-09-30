using UnityEngine;

namespace MiniTank
{
    /// <summary>
    /// Oyunun tüm seslerini kodla üretir (ses dosyası gerekmez).
    /// Gerçekçilik için katmanlı sentez kullanılır: basınç dalgası (ilk "şak"), gövde gürültüsü,
    /// alçak frekanslı gümbürtü, mekanik parçalar ve açık alan yankısı.
    /// İleride gerçek kayıtlarla değiştirmek için GameAudio'daki klipleri değiştirmek yeterli.
    /// </summary>
    public static class SoundSynth
    {
        /// <summary>Efektler için yüksek kalite (keskin tiz), döngüler ve arayüz için düşük örnekleme.</summary>
        public const int HiRate = 44100;
        const int Rate = 22050;

        static System.Random rng = new System.Random(1234);
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static AudioClip Make(string name, float[] data, int rate = Rate, float peak = 0.9f, bool loop = false, float drive = 0f)
        {
            RemoveDc(data);
            if (drive > 0f) Saturate(data, drive);
            if (!loop) FadeOut(data, rate, 0.08f);
            Normalize(data, peak);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>
        /// Yumuşak doyum (kayıt stüdyosu kompresörü gibi): tepe darbesini bastırıp gövdeyi öne çıkarır,
        /// ses daha dolgun ve yüksek algılanır.
        /// </summary>
        static void Saturate(float[] d, float drive)
        {
            Normalize(d, 1f);
            float norm = (float)System.Math.Tanh(drive);
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * drive) / norm;
        }

        /// <summary>Tek seferlik seslerin sonunda "tık" olmasın diye kısa sönme.</summary>
        static void FadeOut(float[] d, int rate, float seconds)
        {
            int len = Mathf.Min(d.Length, (int)(rate * seconds));
            for (int i = 0; i < len; i++) d[d.Length - 1 - i] *= (float)i / len;
        }

        /// <summary>Hoparlörü zorlayan DC kaymasını siler (çok alçak kesimli yüksek geçiren).</summary>
        static void RemoveDc(float[] d)
        {
            float prevIn = 0f, prevOut = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float y = d[i] - prevIn + 0.9995f * prevOut;
                prevIn = d[i];
                prevOut = y;
                d[i] = y;
            }
        }

        // ================================================================== Filtreler

        /// <summary>RBJ biquad filtre (alçak/yüksek/bant geçiren).</summary>
        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public static Biquad LowPass(float freq, float q, int rate) => Make(0, freq, q, rate);
            public static Biquad HighPass(float freq, float q, int rate) => Make(1, freq, q, rate);
            public static Biquad BandPass(float freq, float q, int rate) => Make(2, freq, q, rate);

            static Biquad Make(int type, float freq, float q, int rate)
            {
                freq = Mathf.Clamp(freq, 10f, rate * 0.45f);
                float w = 2f * Mathf.PI * freq / rate;
                float cs = Mathf.Cos(w), sn = Mathf.Sin(w);
                float alpha = sn / (2f * q);
                float a0 = 1f + alpha;
                var f = new Biquad();
                switch (type)
                {
                    case 0:
                        f.b0 = (1f - cs) / 2f; f.b1 = 1f - cs; f.b2 = (1f - cs) / 2f; break;
                    case 1:
                        f.b0 = (1f + cs) / 2f; f.b1 = -(1f + cs); f.b2 = (1f + cs) / 2f; break;
                    default:
                        f.b0 = alpha; f.b1 = 0f; f.b2 = -alpha; break;
                }
                f.a1 = -2f * cs; f.a2 = 1f - alpha;
                f.b0 /= a0; f.b1 /= a0; f.b2 /= a0; f.a1 /= a0; f.a2 /= a0;
                return f;
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        /// <summary>
        /// Açık alan yankısı: birkaç erken yansıma (bina/tepe) + Schroeder difüz kuyruk.
        /// Yankı tizleri yutar (uzak yansımalar boğuk gelir).
        /// </summary>
        static void AddReverb(float[] d, int rate, float wet, float decay, float damping, float[] earlyTimes, float[] earlyGains)
        {
            int n = d.Length;
            var dry = (float[])d.Clone();

            // Erken yansımalar
            if (earlyTimes != null)
            {
                for (int e = 0; e < earlyTimes.Length; e++)
                {
                    int delay = (int)(earlyTimes[e] * rate);
                    var lp = Biquad.LowPass(Mathf.Lerp(3500f, 900f, e / (float)Mathf.Max(1, earlyTimes.Length - 1)), 0.7f, rate);
                    for (int i = 0; i + delay < n; i++)
                        d[i + delay] += lp.Process(dry[i]) * earlyGains[e];
                }
            }

            // Difüz kuyruk: 4 paralel tarak + 2 seri tüm-geçiren
            float scale = rate / 44100f;
            int[] combLen = { (int)(1557 * scale), (int)(1617 * scale), (int)(1491 * scale), (int)(1422 * scale) };
            int[] apLen = { (int)(225 * scale), (int)(556 * scale) };
            var tail = new float[n];
            for (int c = 0; c < combLen.Length; c++)
            {
                int len = Mathf.Max(8, combLen[c] * 3); // açık alan: uzun yankı aralığı
                var buf = new float[len];
                int idx = 0;
                float filt = 0f;
                float fb = Mathf.Pow(0.001f, len / (decay * rate)); // decay saniyede -60 dB
                for (int i = 0; i < n; i++)
                {
                    float y = buf[idx];
                    filt = y * (1f - damping) + filt * damping;
                    buf[idx] = dry[i] + filt * fb;
                    idx = (idx + 1) % len;
                    tail[i] += y;
                }
            }
            for (int a = 0; a < apLen.Length; a++)
            {
                int len = Mathf.Max(4, apLen[a]);
                var buf = new float[len];
                int idx = 0;
                for (int i = 0; i < n; i++)
                {
                    float b = buf[idx];
                    float y = -tail[i] + b;
                    buf[idx] = tail[i] + b * 0.5f;
                    idx = (idx + 1) % len;
                    tail[i] = y;
                }
            }
            for (int i = 0; i < n; i++) d[i] += tail[i] * wet * 0.25f;
        }

        /// <summary>Kısa, yumuşak başlangıçlı zarf (tık sesini önler).</summary>
        static float Attack(float t, float time) => time <= 0f ? 1f : Mathf.Clamp01(t / time);

        /// <summary>Hızlı sönümlü bir basınç dalgası ("N-dalgası"): patlamanın ilk sert darbesi.</summary>
        static float NWave(float t, float duration)
        {
            if (t < 0f || t > duration) return 0f;
            float x = t / duration;
            return (1f - 2f * x) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, x * 6f) * 0.5f);
        }

        // ================================================================== Silah sesleri

        /// <summary>
        /// Top atışı. Katmanlar: basınç darbesi, tiz çatlama, gövde gürültüsü, alçalan gümbürtü,
        /// namlu çıkışı "fışş"ı ve açık alan yankısı. distant=true ise uzaktan duyulan boğuk hali.
        /// </summary>
        public static AudioClip Cannon(int variant = 0, bool distant = false)
        {
            int rate = HiRate;
            float len = distant ? 3.2f : 2.6f;
            int n = (int)(rate * len);
            var d = new float[n];
            rng = new System.Random(100 + variant * 17 + (distant ? 7 : 0));

            var crackBp = Biquad.BandPass(Rand(1800f, 2600f), 0.9f, rate);
            var bodyLp = Biquad.LowPass(Rand(380f, 480f), 0.8f, rate);
            var bodyLp2 = Biquad.LowPass(900f, 0.7f, rate);
            var hissBp = Biquad.BandPass(4200f, 0.6f, rate);
            float boomStart = Rand(62f, 72f), boomEnd = Rand(30f, 36f);
            float phase = 0f, phase2 = 0f;
            float nwave = Rand(0.0035f, 0.005f);

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float noise = Noise();

                float blast = NWave(t, nwave) * 1.6f;
                float crack = crackBp.Process(noise) * Mathf.Exp(-t * 55f) * 2.2f;
                float bodyNoise = bodyLp2.Process(bodyLp.Process(noise));
                float body = bodyNoise * Attack(t, 0.002f) * Mathf.Exp(-t * 4.2f) * 5.5f;

                float freq = boomEnd + (boomStart - boomEnd) * Mathf.Exp(-t * 6f);
                phase += 2f * Mathf.PI * freq / rate;
                phase2 += 2f * Mathf.PI * freq * 2.03f / rate;
                float boom = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase2)) * Attack(t, 0.004f) * Mathf.Exp(-t * 3.2f);

                // Namludan çıkan gazın kısa fışşırtısı
                float hiss = hissBp.Process(noise) * Mathf.Exp(-t * 18f) * 0.6f;

                d[i] = blast + crack + body + boom * 0.95f + hiss;
            }

            if (distant)
            {
                // Uzaktan: tizler havada söner, gümbürtü ve yankı kalır
                var lp1 = Biquad.LowPass(700f, 0.7f, rate);
                var lp2 = Biquad.LowPass(900f, 0.7f, rate);
                for (int i = 0; i < n; i++) d[i] = lp2.Process(lp1.Process(d[i]));
                AddReverb(d, rate, 1.4f, 2.4f, 0.55f,
                    new[] { 0.09f, 0.21f, 0.38f, 0.62f }, new[] { 0.45f, 0.35f, 0.25f, 0.15f });
            }
            else
            {
                AddReverb(d, rate, 0.9f, 1.8f, 0.45f,
                    new[] { 0.07f, 0.16f, 0.29f, 0.47f }, new[] { 0.32f, 0.24f, 0.16f, 0.09f });
            }
            return Make(distant ? "CannonFar" + variant : "Cannon" + variant, d, rate, 0.95f, false, distant ? 1.5f : 2.8f);
        }

        /// <summary>
        /// Tank imhası: patlama darbesi, ateş topu gürültüsü, yere düşen hurda parçaları,
        /// metal çınlamaları, alçak sarsıntı ve uzun yankı.
        /// </summary>
        public static AudioClip Explosion(int variant = 0)
        {
            int rate = HiRate;
            int n = (int)(rate * 3.4f);
            var d = new float[n];
            rng = new System.Random(300 + variant * 31);

            var bodyLp = Biquad.LowPass(420f, 0.8f, rate);
            var bodyLp2 = Biquad.LowPass(600f, 0.7f, rate);
            var roarBp = Biquad.BandPass(180f, 0.7f, rate);
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float noise = Noise();
                float blast = NWave(t, 0.006f) * 1.8f;
                float body = bodyLp2.Process(bodyLp.Process(noise)) * Attack(t, 0.004f) * Mathf.Exp(-t * 1.6f) * 7f;
                float roar = roarBp.Process(noise) * Attack(t, 0.05f) * Mathf.Exp(-t * 1.1f) * 1.4f; // alev uğultusu
                phase += 2f * Mathf.PI * (26f + 30f * Mathf.Exp(-t * 3f)) / rate;
                float rumble = Mathf.Sin(phase) * Attack(t, 0.01f) * Mathf.Exp(-t * 1.7f);
                d[i] = blast + body + roar + rumble * 1.1f;
            }

            // Hurda: yere düşen parçaların kısa, rastgele darbeleri (sıklığı zamanla azalır)
            int debris = 70;
            for (int k = 0; k < debris; k++)
            {
                float t0 = 0.15f + Mathf.Pow(Rand(0f, 1f), 1.8f) * 2.2f;
                int start = (int)(t0 * rate);
                float amp = Rand(0.15f, 0.6f) * Mathf.Exp(-t0 * 0.9f);
                bool metal = rng.NextDouble() < 0.35;
                float f = metal ? Rand(1800f, 4200f) : Rand(600f, 2200f);
                var bp = Biquad.BandPass(f, metal ? 8f : 1.5f, rate);
                int len = (int)(rate * (metal ? 0.25f : 0.05f));
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = (float)i / rate;
                    float env = Mathf.Exp(-t * (metal ? 22f : 90f));
                    float s = metal ? Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + bp.Process(Noise()) * 0.4f : bp.Process(Noise()) * 2f;
                    d[start + i] += s * env * amp;
                }
            }

            AddReverb(d, rate, 1.1f, 2.6f, 0.5f,
                new[] { 0.08f, 0.19f, 0.34f, 0.55f }, new[] { 0.35f, 0.26f, 0.18f, 0.1f });
            return Make("Explosion" + variant, d, rate, 0.95f, false, 2.5f);
        }

        /// <summary>
        /// Zırha isabet: kalın çeliğin kısa, tok "dank" sesi (uyumsuz rezonanslar) + darbe gürültüsü.
        /// </summary>
        public static AudioClip HitMetal(int variant = 0)
        {
            int rate = HiRate;
            int n = (int)(rate * 0.9f);
            var d = new float[n];
            rng = new System.Random(500 + variant * 13);
            // Kalın plaka: tok alçak mod + uyumsuz üst modlar
            float baseF = Rand(150f, 190f);
            float[] ratios = { 1f, 2.32f, 3.91f, 5.4f, 8.9f, 13.3f };
            float[] decays = { 9f, 12f, 16f, 22f, 30f, 40f };
            float[] gains = { 1f, 0.7f, 0.5f, 0.35f, 0.25f, 0.18f };
            var strikeBp = Biquad.BandPass(Rand(2500f, 3500f), 0.8f, rate);
            var thudLp = Biquad.LowPass(250f, 0.8f, rate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float noise = Noise();
                float s = 0f;
                for (int k = 0; k < ratios.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * baseF * ratios[k] * t) * Mathf.Exp(-t * decays[k]) * gains[k];
                float strike = strikeBp.Process(noise) * Mathf.Exp(-t * 60f) * 3f;
                float thud = thudLp.Process(noise) * Mathf.Exp(-t * 25f) * 6f;
                d[i] = s * Attack(t, 0.0008f) * 0.8f + strike + thud + NWave(t, 0.002f);
            }
            AddReverb(d, rate, 0.5f, 0.9f, 0.4f, new[] { 0.06f, 0.14f }, new[] { 0.2f, 0.12f });
            return Make("HitMetal" + variant, d, rate, 0.9f, false, 1.8f);
        }

        /// <summary>Sekme: önden zırha çarpıp seken merminin ıslıklı vızıltısı.</summary>
        public static AudioClip Ricochet(int variant = 0)
        {
            int rate = HiRate;
            int n = (int)(rate * 0.9f);
            var d = new float[n];
            rng = new System.Random(700 + variant * 7);
            float f0 = Rand(2600f, 3400f), f1 = Rand(900f, 1300f);
            float phase = 0f;
            var ping = Biquad.BandPass(Rand(3000f, 4000f), 4f, rate);
            var airBp = Biquad.BandPass(2200f, 1.2f, rate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float x = Mathf.Clamp01(t / 0.7f);
                float f = Mathf.Lerp(f0, f1, x * x) * (1f + 0.015f * Mathf.Sin(2f * Mathf.PI * 32f * t));
                phase += 2f * Mathf.PI * f / rate;
                float env = Attack(t, 0.03f) * Mathf.Exp(-t * 3.5f);
                float whine = Mathf.Sin(phase) * env * 0.5f + airBp.Process(Noise()) * env * 0.6f;
                float hit = ping.Process(Noise()) * Mathf.Exp(-t * 40f) * 3f + NWave(t, 0.0015f) * 0.8f;
                d[i] = whine + hit;
            }
            AddReverb(d, rate, 0.6f, 1.2f, 0.4f, new[] { 0.08f }, new[] { 0.2f });
            return Make("Ricochet" + variant, d, rate, 0.8f);
        }

        /// <summary>Yere/duvara isabet: toprak gümlemesi + saçılan toprak/taş.</summary>
        public static AudioClip ImpactGround(int variant = 0)
        {
            int rate = HiRate;
            int n = (int)(rate * 1.4f);
            var d = new float[n];
            rng = new System.Random(900 + variant * 11);
            var lp = Biquad.LowPass(Rand(320f, 420f), 0.8f, rate);
            var mid = Biquad.BandPass(900f, 0.9f, rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float noise = Noise();
                phase += 2f * Mathf.PI * (48f + 40f * Mathf.Exp(-t * 20f)) / rate;
                float thud = Mathf.Sin(phase) * Attack(t, 0.002f) * Mathf.Exp(-t * 9f);
                float body = lp.Process(noise) * Attack(t, 0.002f) * Mathf.Exp(-t * 7f) * 5f;
                float crack = mid.Process(noise) * Mathf.Exp(-t * 35f) * 1.5f;
                d[i] = NWave(t, 0.003f) * 1.2f + thud + body + crack;
            }
            // Geri düşen toprak ve taşlar
            for (int k = 0; k < 40; k++)
            {
                float t0 = 0.08f + Mathf.Pow(Rand(0f, 1f), 1.5f) * 0.8f;
                int start = (int)(t0 * rate);
                var bp = Biquad.BandPass(Rand(1500f, 5000f), 1.2f, rate);
                float amp = Rand(0.05f, 0.25f) * Mathf.Exp(-t0 * 2.5f);
                int len = (int)(rate * 0.03f);
                for (int i = 0; i < len && start + i < n; i++)
                    d[start + i] += bp.Process(Noise()) * Mathf.Exp(-i / (float)rate * 120f) * amp * 3f;
            }
            AddReverb(d, rate, 0.7f, 1.3f, 0.5f, new[] { 0.07f, 0.17f }, new[] { 0.25f, 0.15f });
            return Make("ImpactGround" + variant, d, rate, 0.85f, false, 2.2f);
        }

        /// <summary>Yakından geçen merminin vızıltısı (Doppler etkili).</summary>
        public static AudioClip Flyby()
        {
            int rate = HiRate;
            int n = (int)(rate * 0.7f);
            var d = new float[n];
            rng = new System.Random(1100);
            var bp = Biquad.BandPass(1500f, 1.5f, rate);
            var lp = Biquad.LowPass(3000f, 0.7f, rate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                // Geçiş anı 0.25 sn: yaklaşırken yükselir, uzaklaşırken hızla söner ve kalınlaşır
                float x = (t - 0.25f) / 0.08f;
                float env = 1f / (1f + x * x);
                float f = t < 0.25f ? 1.25f : 0.75f;
                d[i] = lp.Process(bp.Process(Noise()) * env * 3f) * f + Mathf.Sin(2f * Mathf.PI * 420f * f * t) * env * 0.15f;
            }
            return Make("Flyby", d, rate, 0.7f);
        }

        /// <summary>Topa yeni mermi sürülmesi: mekanik "şak-çunk" (sadece oyuncunun tankında).</summary>
        public static AudioClip Reload()
        {
            int rate = HiRate;
            int n = (int)(rate * 0.55f);
            var d = new float[n];
            rng = new System.Random(1300);
            float[] hits = { 0f, 0.18f, 0.23f };
            float[] freqs = { 1400f, 520f, 2600f };
            float[] amps = { 0.6f, 1f, 0.35f };
            for (int h = 0; h < hits.Length; h++)
            {
                int start = (int)(hits[h] * rate);
                var bp = Biquad.BandPass(freqs[h], 3f, rate);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / rate;
                    float env = Mathf.Exp(-t * (h == 1 ? 30f : 45f));
                    d[i] += (bp.Process(Noise()) * 2f + Mathf.Sin(2f * Mathf.PI * freqs[h] * 0.5f * t) * 0.5f) * env * amps[h];
                }
            }
            return Make("Reload", d, rate, 0.8f);
        }

        // ================================================================== Döngüler

        /// <summary>
        /// Dizel motor (döngü): silindir ateşlemeleri tek tek darbe olarak üretilir, egzoz gürültüsü
        /// ve mekanik uğultu eklenir. Darbeler döngünün sonunda başa sarıldığı için kesintisizdir.
        /// </summary>
        public static AudioClip Engine()
        {
            int rate = Rate;
            float seconds = 2f;
            int n = (int)(rate * seconds);
            var d = new float[n];
            rng = new System.Random(1500);

            const float firing = 36f;             // saniyede ateşleme (rölanti üstü)
            int pulses = (int)(firing * seconds); // tam sayı: döngü dikişsiz
            float period = seconds / pulses;
            float[] cylGain = new float[12];
            for (int c = 0; c < cylGain.Length; c++) cylGain[c] = Rand(0.75f, 1.1f);

            for (int p = 0; p < pulses; p++)
            {
                float t0 = p * period + Rand(-0.0015f, 0.0015f);
                int start = (int)(t0 * rate);
                float g = cylGain[p % cylGain.Length];
                var lp = Biquad.LowPass(Rand(500f, 700f), 0.9f, rate);
                int len = (int)(rate * period * 2.2f);
                for (int i = 0; i < len; i++)
                {
                    float t = (float)i / rate;
                    float env = Attack(t, 0.0015f) * Mathf.Exp(-t * 55f);
                    float s = lp.Process(Noise()) * 2.2f + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.8f;
                    int idx = (start + i) % n;
                    if (idx < 0) idx += n;
                    d[idx] += s * env * g;
                }
            }

            // Egzoz gürültüsü ve mekanik uğultu (tam dalga sayılarıyla, döngü dikişsiz)
            var exLp = Biquad.LowPass(260f, 0.7f, rate);
            float[] ex = new float[n + rate / 4];
            for (int i = 0; i < ex.Length; i++) ex[i] = exLp.Process(Noise());
            int fade = rate / 4;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float noise = ex[i];
                if (i < fade) noise = Mathf.Lerp(ex[n + i], ex[i], (float)i / fade); // gürültü dikişi
                float whine = Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.05f + Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.02f;
                d[i] += noise * 0.55f + whine;
            }
            return Make("Engine", d, rate, 0.8f, true, 1.5f);
        }

        /// <summary>
        /// Palet şıkırtısı (döngü): palet baklalarının dişliye çarpması. Hızla birlikte perdesi ve sesi artar.
        /// </summary>
        public static AudioClip Tracks()
        {
            int rate = Rate;
            float seconds = 2f;
            int n = (int)(rate * seconds);
            var d = new float[n];
            rng = new System.Random(1700);
            int clicks = 28; // saniyede 14 bakla
            for (int k = 0; k < clicks; k++)
            {
                float t0 = k * seconds / clicks + Rand(-0.006f, 0.006f);
                int start = (int)(t0 * rate);
                float amp = Rand(0.5f, 1f);
                float f = Rand(1100f, 1700f);
                var bp = Biquad.BandPass(f, 2.5f, rate);
                var ring = Biquad.BandPass(f * 2.3f, 10f, rate);
                int len = (int)(rate * 0.06f);
                for (int i = 0; i < len; i++)
                {
                    float t = (float)i / rate;
                    float noise = Noise();
                    float s = bp.Process(noise) * Mathf.Exp(-t * 90f) * 2f + ring.Process(noise) * Mathf.Exp(-t * 40f) * 1.5f;
                    int idx = (start + i) % n;
                    if (idx < 0) idx += n;
                    d[idx] += s * amp;
                }
            }
            // Alttaki gıcırtı ve yuvarlanma gürültüsü
            var lp = Biquad.LowPass(180f, 0.7f, rate);
            float[] rum = new float[n + rate / 4];
            for (int i = 0; i < rum.Length; i++) rum[i] = lp.Process(Noise());
            int fade = rate / 4;
            for (int i = 0; i < n; i++)
            {
                float r = i < fade ? Mathf.Lerp(rum[n + i], rum[i], (float)i / fade) : rum[i];
                d[i] += r * 0.9f;
            }
            return Make("Tracks", d, rate, 0.8f, true);
        }

        /// <summary>Rüzgar ambiyansı (döngü): yavaşça değişen, filtreli gürültü.</summary>
        public static AudioClip Wind()
        {
            int rate = Rate;
            int n = rate * 8;
            var d = new float[n + rate];
            rng = new System.Random(1900);
            var lp = Biquad.LowPass(500f, 0.6f, rate);
            var bp = Biquad.BandPass(700f, 3f, rate);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / rate;
                float noise = Noise();
                float gust = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / 8f) + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 2f + 1f);
                // Hafif ıslık: rüzgarın engellerden geçişi
                float whistle = bp.Process(noise) * 0.35f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t / 4f));
                d[i] = lp.Process(noise) * gust * 2f + whistle;
            }
            var outD = new float[n];
            int fade = rate;
            for (int i = 0; i < n; i++)
                outD[i] = i < fade ? Mathf.Lerp(d[n + i], d[i], (float)i / fade) : d[i];
            return Make("Wind", outD, rate, 0.7f, true);
        }

        // ================================================================== Arayüz ve yetenekler

        public static AudioClip Click()
        {
            int n = (int)(Rate * 0.06f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 1300f * t) * Mathf.Exp(-t * 60f);
            }
            return Make("Click", d);
        }

        /// <summary>Kısa melodik onay sesi (imha, asist, lig atlama).</summary>
        public static AudioClip Chime(string name, params float[] notes)
        {
            float noteLen = 0.13f;
            int n = (int)(Rate * (noteLen * notes.Length + 0.4f));
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(Rate * noteLen * k);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 7f);
                    d[i] += (Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[k] * t)) * env;
                }
            }
            return Make(name, d);
        }

        /// <summary>Menü müziği: yumuşak akorlar ve bas (16 saniyelik döngü).</summary>
        public static AudioClip MenuMusic()
        {
            // Am - F - C - G
            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f, 110.00f },
                new[] { 174.61f, 220.00f, 261.63f, 87.31f },
                new[] { 196.00f, 261.63f, 329.63f, 130.81f },
                new[] { 196.00f, 246.94f, 293.66f, 98.00f },
            };
            float chordLen = 4f;
            int n = (int)(Rate * chordLen * chords.Length);
            var d = new float[n];
            for (int c = 0; c < chords.Length; c++)
            {
                int start = (int)(Rate * chordLen * c);
                int len = (int)(Rate * (chordLen + 0.8f)); // bir sonrakine hafif taşar
                for (int i = 0; i < len; i++)
                {
                    int idx = (start + i) % n; // döngünün başına sarar: kesintisiz
                    float t = (float)i / Rate;
                    float env = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((chordLen + 0.8f - t) / 1.2f);
                    float s = 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        float f = chords[c][k];
                        s += Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 1.003f * t);
                    }
                    float bass = Mathf.Sin(2f * Mathf.PI * chords[c][3] * t) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 2f * t));
                    int step = (int)(t * 4f) % 4;
                    float at = t - Mathf.Floor(t * 4f) / 4f;
                    float arp = Mathf.Sin(2f * Mathf.PI * chords[c][step % 3] * 2f * t) * Mathf.Exp(-at * 9f) * 0.5f;
                    d[idx] += (s * 0.25f + bass * 0.5f + arp) * env;
                }
            }
            Normalize(d, 0.6f);
            var clip = AudioClip.Create("MenuMusic", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Nitro: turbo ıslığı ve egzoz patlaması.</summary>
        public static AudioClip Whoosh()
        {
            int rate = HiRate;
            int n = (int)(rate * 1.3f);
            var d = new float[n];
            rng = new System.Random(2100);
            float phase = 0f;
            var lp = Biquad.LowPass(300f, 0.8f, rate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float cutoff = Mathf.Lerp(400f, 5000f, Mathf.Clamp01(t / 0.6f));
                float env = Attack(t, 0.1f) * Mathf.Exp(-Mathf.Max(0f, t - 0.6f) * 4f);
                phase += 2f * Mathf.PI * Mathf.Lerp(900f, 2800f, Mathf.Clamp01(t / 0.7f)) / rate;
                float turbo = Mathf.Sin(phase) * 0.15f * env;
                float air = Noise() * env * Mathf.Clamp01(cutoff / 5000f) * 0.5f;
                float puff = lp.Process(Noise()) * Mathf.Exp(-t * 10f) * 4f;
                d[i] = turbo + air + puff;
            }
            return Make("Whoosh", d, rate, 0.8f);
        }

        /// <summary>Topçu atışı: düşen merminin Doppler etkili ıslığı.</summary>
        public static AudioClip Whistle()
        {
            int rate = HiRate;
            int n = (int)(rate * 1.5f);
            var d = new float[n];
            rng = new System.Random(2300);
            float phase = 0f;
            var bp = Biquad.BandPass(1200f, 2f, rate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float x = t / 1.5f;
                float f = Mathf.Lerp(1900f, 700f, x * x);
                phase += 2f * Mathf.PI * f / rate;
                float env = Attack(t, 0.25f) * Mathf.Clamp01((1.5f - t) / 0.08f) * (0.4f + 0.6f * x);
                d[i] = (Mathf.Sin(phase) * 0.7f + bp.Process(Noise()) * 0.8f) * env;
            }
            return Make("Whistle", d, rate, 0.7f);
        }
    }
}
