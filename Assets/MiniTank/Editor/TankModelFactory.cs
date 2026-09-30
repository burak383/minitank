using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Detaylı prosedürel tank modelleri: tek tek palet baklaları, dönüş makaraları, dişli, bölümlü yan etekler,
    /// ek zırh blokları, nişangah, periskoplar, depo sepeti, ısı kılıflı namlu, yıpranmış boya dokusu ve normal haritası.
    /// </summary>
    public static partial class MiniTankBuilder
    {
        class TankMaterials
        {
            public Material body;    // takım gövde boyası (adı TankBody) - yıpranma dokulu
            public Material marker;  // parlak takım rengi (adı TankMarker)
            public Material steel;   // koyu metal
            public Material track;   // palet çeliği
            public Material rubber;  // tekerlek lastiği
            public Material grille;  // ızgara / çok koyu metal
            public Material glass;   // nişangah / periskop camı
            public Material light;   // far camı
            public Material canvas;  // branda, çanta
            public Material dust;    // toz parçacığı
        }

        static TankMaterials CreateTankMaterials(Material particle)
        {
            var wear = TextureFactory.Create("TankWear", TextureFactory.Kind.Wear, 21, 512);
            var wearN = TextureFactory.CreateNormalMap("TankWear_N", TextureFactory.Kind.Wear, 21, 6f, 512);

            var m = new TankMaterials
            {
                body = CreateTexturedMaterial("TankBody", new Color(0.8f, 0.8f, 0.8f), wear, new Vector2(1.5f, 1.5f), 0.3f),
                marker = CreateMaterial("TankMarker", Color.white, 0.4f, 0f),
                steel = CreateTexturedMaterial("TankSteel", new Color(0.24f, 0.24f, 0.25f), wear, new Vector2(2f, 2f), 0.45f),
                track = CreateTexturedMaterial("TankTrack", new Color(0.2f, 0.19f, 0.18f), wear, new Vector2(3f, 3f), 0.35f),
                rubber = CreateMaterial("TankRubber", new Color(0.08f, 0.08f, 0.08f), 0.15f, 0f),
                grille = CreateMaterial("TankGrille", new Color(0.07f, 0.07f, 0.08f), 0.2f, 0.4f),
                glass = CreateMaterial("TankGlass", new Color(0.15f, 0.3f, 0.35f), 0.6f, 0.2f),
                light = CreateMaterial("TankLight", new Color(0.85f, 0.85f, 0.75f), 0.4f, 0f),
                canvas = CreateMaterial("TankCanvas", new Color(0.42f, 0.4f, 0.3f), 0.1f, 0f),
                dust = particle,
            };
            m.track.SetFloat("_Metallic", 0.5f);
            m.steel.SetFloat("_Metallic", 0.55f);
            foreach (var mat in new[] { m.body, m.steel, m.track })
            {
                if (wearN == null || !mat.HasProperty("_BumpMap")) continue;
                mat.SetTexture("_BumpMap", wearN);
                mat.SetTextureScale("_BumpMap", mat.GetTextureScale("_BaseMap"));
                mat.SetFloat("_BumpScale", 0.6f);
                mat.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(mat);
            }
            if (m.light.HasProperty("_EmissionColor"))
            {
                m.light.EnableKeyword("_EMISSION");
                m.light.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.8f) * 0.25f);
            }
            return m;
        }

        /// <summary>Paletin dış yüzeyindeki baklaları (tırnaklı çelik plakalar) yol boyunca dizer.</summary>
        static void AddTrackLinks(MeshKit kit, Material mat, float x, float length, float height, float width)
        {
            float r = height / 2f;
            float half = length / 2f - r;
            float straight = half * 2f;
            float arc = Mathf.PI * r;
            float perimeter = straight * 2f + arc * 2f;
            float pitch = 0.2f;
            int count = Mathf.Max(8, Mathf.RoundToInt(perimeter / pitch));
            for (int i = 0; i < count; i++)
            {
                float d = perimeter * i / count;
                Vector2 p, n;
                if (d < straight) { p = new Vector2(half - d, 0f); n = Vector2.down; }                       // alt düz: önden arkaya
                else if ((d -= straight) < arc)
                {
                    float a = -Mathf.PI / 2f - d / r;                                                         // arka yay
                    n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    p = new Vector2(-half, r) + n * r;
                }
                else if ((d -= arc) < straight) { p = new Vector2(-half + d, height); n = Vector2.up; }       // üst düz
                else
                {
                    d -= straight;
                    float a = Mathf.PI / 2f - d / r;                                                          // ön yay
                    n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    p = new Vector2(half, r) + n * r;
                }
                float angle = Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg; // bakla normalini (y) yola dik çevir
                var rot = Quaternion.Euler(angle, 0f, 0f);
                Vector3 c = new Vector3(x, p.y + n.y * 0.03f + 0.07f, p.x + n.x * 0.03f); // yerle temas: tırnaklar zemine değsin
                kit.Box(mat, c, new Vector3(width + 0.06f, 0.07f, pitch * 0.78f), rot);
                // Tırnak (grouser): baklanın ortasında yükselti
                kit.Box(mat, c + new Vector3(0f, n.y, n.x) * 0.045f, new Vector3(width * 0.8f, 0.04f, 0.05f), rot);
            }
        }

        static GameObject CreateTankPrefab(TankSpec s, TankMaterials m, GameObject flash, GameObject explosion)
        {
            bool light = s.id == "Hafif", heavy = s.id == "Agir", arty = s.id == "Topcu";

            var root = new GameObject("Tank_" + s.id);

            float L = s.hull.z;
            float trackW = Mathf.Clamp(s.hull.x * 0.22f, 0.45f, 0.75f);
            float trackH = Mathf.Clamp(L * 0.2f, 0.7f, 1.1f);
            float inner = s.hull.x / 2f;
            float trackX = inner + trackW / 2f;
            float hb = trackH * 0.6f;               // gövde alt kenarı
            float ht = hb + s.hull.y;               // gövde üst kenarı
            float hullW = s.hull.x + trackW * 2f + 0.25f;
            float trackLen = L * 1.02f;

            // ---------------------------------------------------------------- Gövde
            var hull = new MeshKit();
            var hullProfile = new[]
            {
                new Vector2(-L / 2f + 0.25f, hb),
                new Vector2(L / 2f - 0.5f, hb),
                new Vector2(L / 2f, hb + s.hull.y * 0.45f),
                new Vector2(L / 2f - L * 0.28f, ht),
                new Vector2(-L / 2f + 0.15f, ht),
                new Vector2(-L / 2f, ht - s.hull.y * 0.35f),
            };
            hull.Add(Shapes.Prism(hullProfile, hullW), m.body, Vector3.zero);
            // Alt karın
            hull.Box(m.body, new Vector3(0f, (0.35f + hb) / 2f, 0f), new Vector3(s.hull.x, hb - 0.35f, L * 0.85f));

            // Paletler: iç gövde (koyu) + tek tek çelik baklalar
            var trackMesh = Shapes.Prism(Shapes.Stadium(trackLen - 0.06f, trackH - 0.06f, 6), trackW - 0.04f);
            for (int side = -1; side <= 1; side += 2)
            {
                hull.Add(trackMesh, m.grille, new Vector3(side * trackX, 0.1f, 0f));
                AddTrackLinks(hull, m.track, side * trackX, trackLen, trackH, trackW);
            }

            // Çamurluklar: paletin üstünde ince plakalar (ön ve arka uçta hafif eğimli)
            float fenderY = trackH + 0.17f;
            for (int side = -1; side <= 1; side += 2)
            {
                float fx = side * trackX;
                hull.Box(m.body, new Vector3(fx, fenderY, 0f), new Vector3(trackW + 0.12f, 0.04f, L * 0.86f));
                hull.Box(m.body, new Vector3(fx, fenderY - 0.1f, L * 0.47f), new Vector3(trackW + 0.12f, 0.04f, L * 0.1f), Quaternion.Euler(20f, 0f, 0f));
                hull.Box(m.body, new Vector3(fx, fenderY - 0.08f, -L * 0.47f), new Vector3(trackW + 0.12f, 0.04f, L * 0.08f), Quaternion.Euler(-15f, 0f, 0f));
            }

            // Yan etekler: bölümlü zırh plakaları (hafif tankta yok)
            if (!light)
            {
                int panels = heavy ? 6 : 5;
                float skirtH = heavy ? 0.62f : 0.42f;
                float panelL = L * 0.84f / panels;
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < panels; i++)
                    {
                        float z = -L * 0.42f + panelL * (i + 0.5f);
                        Vector3 c = new Vector3(side * (hullW / 2f + 0.03f), fenderY - skirtH / 2f + 0.02f, z);
                        hull.Box(m.body, c, new Vector3(0.05f, skirtH, panelL - 0.04f));
                        // cıvatalar
                        for (int b = -1; b <= 1; b += 2)
                            hull.CylinderX(m.steel, c + new Vector3(side * 0.03f, skirtH * 0.32f, b * panelL * 0.32f), 0.025f, 0.03f);
                    }
            }

            // Ön eğik zırh: sürücü kapağı ve periskoplar
            float slope = Mathf.Atan2(s.hull.y * 0.55f, L * 0.28f) * Mathf.Rad2Deg;
            float glacisZ = L / 2f - L * 0.14f;
            float glacisY = hb + s.hull.y * 0.72f;
            var slopeRot = Quaternion.Euler(-slope, 0f, 0f);
            hull.Box(m.steel, new Vector3(-hullW * 0.18f, ht - 0.02f, L / 2f - L * 0.29f), new Vector3(0.55f, 0.06f, 0.5f));
            for (int i = -1; i <= 1; i++)
                hull.Box(m.glass, new Vector3(-hullW * 0.18f + i * 0.14f, ht + 0.03f, L / 2f - L * 0.29f + 0.28f), new Vector3(0.1f, 0.06f, 0.05f));

            // Ön yedek palet baklaları (eğik zırhın üstünde)
            for (int i = -1; i <= 1; i++)
                hull.Box(m.track, new Vector3(hullW * 0.12f + i * 0.34f, glacisY + 0.04f, glacisZ), new Vector3(0.3f, 0.06f, 0.2f), slopeRot);

            // Farlar ve koruyucu kafesleri
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = new Vector3(side * (hullW / 2f - 0.3f), hb + s.hull.y * 0.62f, L / 2f - L * 0.13f);
                hull.CylinderZ(m.steel, p, 0.11f, 0.12f);
                hull.CylinderZ(m.light, p + new Vector3(0f, 0f, 0.07f), 0.08f, 0.04f);
                hull.Box(m.steel, p + new Vector3(0f, 0.15f, 0.08f), new Vector3(0.26f, 0.025f, 0.025f));
                hull.Box(m.steel, p + new Vector3(side * 0.13f, 0.02f, 0.08f), new Vector3(0.025f, 0.28f, 0.025f));
            }

            // Ön çeki kancaları
            for (int side = -1; side <= 1; side += 2)
                hull.Box(m.steel, new Vector3(side * s.hull.x * 0.3f, hb + 0.08f, L / 2f - 0.42f), new Vector3(0.12f, 0.18f, 0.22f));

            // Yan taraftaki çekme halatları
            for (int side = -1; side <= 1; side += 2)
                hull.CylinderZ(m.grille, new Vector3(side * (hullW / 2f - 0.12f), ht + 0.04f, -L * 0.05f), 0.035f, L * 0.55f);

            // Çamurluk kenarında saklama kutuları
            for (int side = -1; side <= 1; side += 2)
            {
                hull.Box(m.body, new Vector3(side * (hullW / 2f - 0.25f), ht + 0.14f, -L * 0.18f), new Vector3(0.4f, 0.28f, L * 0.22f));
                hull.Box(m.steel, new Vector3(side * (hullW / 2f - 0.25f), ht + 0.29f, -L * 0.18f), new Vector3(0.42f, 0.02f, L * 0.23f));
            }

            // Arka motor güvertesi: çıtalı ızgaralar + tavan tanıma paneli (takım rengi)
            float deckZ = -L / 2f + L * 0.2f;
            for (int i = 0; i < 7; i++)
                hull.Box(m.grille, new Vector3(0f, ht + 0.025f, deckZ - L * 0.09f + i * L * 0.03f), new Vector3(hullW * 0.5f, 0.03f, L * 0.018f));
            hull.Box(m.marker, new Vector3(0f, ht + 0.05f, deckZ + L * 0.16f), new Vector3(hullW * 0.3f, 0.03f, L * 0.1f));

            // Arka: egzoz ızgaraları, benzin bidonları
            for (int side = -1; side <= 1; side += 2)
            {
                hull.Box(m.grille, new Vector3(side * hullW * 0.22f, ht - s.hull.y * 0.3f, -L / 2f - 0.02f), new Vector3(hullW * 0.22f, s.hull.y * 0.28f, 0.05f));
                for (int k = 0; k < 4; k++)
                    hull.Box(m.steel, new Vector3(side * hullW * 0.22f, ht - s.hull.y * 0.42f + k * s.hull.y * 0.08f, -L / 2f - 0.05f), new Vector3(hullW * 0.23f, 0.02f, 0.03f));
                hull.Box(m.canvas, new Vector3(side * hullW * 0.38f, ht - 0.2f, -L / 2f - 0.13f), new Vector3(0.3f, 0.42f, 0.18f));
            }

            hull.Build($"Tank_{s.id}_Hull", "Gövde", root.transform, Vector3.zero, Quaternion.identity);

            // ---------------------------------------------------------------- Tekerlekler
            float wheelR = trackH * 0.36f;
            var wheel = new MeshKit();
            wheel.CylinderX(m.rubber, Vector3.zero, wheelR, 0.14f);          // lastik bandaj
            wheel.CylinderX(m.body, Vector3.zero, wheelR * 0.8f, 0.18f);     // jant
            wheel.CylinderX(m.steel, Vector3.zero, wheelR * 0.3f, 0.24f);    // göbek
            for (int i = 0; i < 6; i++)                                       // hafifletme delikleri (dönüşü gösterir)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 off = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)) * wheelR * 0.55f;
                wheel.CylinderX(m.grille, off, wheelR * 0.11f, 0.2f);
            }
            var wheelMesh = wheel.BuildMesh($"Tank_{s.id}_Wheel", out var wheelMats);

            // Tahrik dişlisi: dişli çember
            var sprocketKit = new MeshKit();
            sprocketKit.CylinderX(m.steel, Vector3.zero, wheelR * 0.85f, 0.2f);
            sprocketKit.CylinderX(m.body, Vector3.zero, wheelR * 0.4f, 0.26f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Vector3 off = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)) * wheelR * 0.9f;
                sprocketKit.Box(m.steel, off, new Vector3(0.16f, wheelR * 0.2f, wheelR * 0.2f), Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f));
            }
            var sprocketMesh = sprocketKit.BuildMesh($"Tank_{s.id}_Sprocket", out var sprocketMats);

            int wheelCount = light ? 4 : heavy ? 7 : 6;
            float wx = inner + trackW * 0.5f;
            float z0 = -L / 2f + trackH * 0.7f, z1 = L / 2f - trackH * 0.7f;
            var leftWheels = new List<Transform>();
            var rightWheels = new List<Transform>();
            var wheelRoot = new GameObject("Tekerlekler").transform;
            wheelRoot.SetParent(root.transform, false);

            for (int side = -1; side <= 1; side += 2)
            {
                var list = side < 0 ? leftWheels : rightWheels;
                float x = side * (wx + trackW * 0.5f + 0.02f);
                for (int i = 0; i < wheelCount; i++)
                {
                    float z = Mathf.Lerp(z0, z1, wheelCount == 1 ? 0.5f : (float)i / (wheelCount - 1));
                    var w = MeshKit.Spawn(wheelMesh, wheelMats, "Tekerlek", wheelRoot, new Vector3(x, wheelR + 0.14f, z), Quaternion.identity);
                    list.Add(w.transform);
                }
                // Öndeki tahrik dişlisi ve arkadaki avare teker
                var sprocket = MeshKit.Spawn(sprocketMesh, sprocketMats, "Dişli", wheelRoot,
                    new Vector3(x, trackH * 0.5f + 0.07f, trackLen / 2f - trackH / 2f), Quaternion.identity);
                list.Add(sprocket.transform);
                var idler = MeshKit.Spawn(wheelMesh, wheelMats, "Avare", wheelRoot,
                    new Vector3(x, trackH * 0.5f + 0.07f, -trackLen / 2f + trackH / 2f), Quaternion.identity);
                idler.transform.localScale = Vector3.one * 0.85f;
                list.Add(idler.transform);
                // Üst dönüş makaraları (paleti taşır)
                int rollers = light ? 2 : 3;
                for (int i = 0; i < rollers; i++)
                {
                    float z = Mathf.Lerp(z0 * 0.6f, z1 * 0.6f, rollers == 1 ? 0.5f : (float)i / (rollers - 1));
                    var roller = MeshKit.Spawn(wheelMesh, wheelMats, "Makara", wheelRoot, new Vector3(x - side * 0.02f, trackH - 0.04f, z), Quaternion.identity);
                    roller.transform.localScale = Vector3.one * 0.38f;
                    list.Add(roller.transform);
                }
            }

            // ---------------------------------------------------------------- Taret
            float tw = s.turret.x, tl = s.turret.z, th = s.turret.y;
            float turretZ = arty ? -L * 0.18f : L * 0.04f;

            var turretPivot = new GameObject("Turret").transform;
            turretPivot.SetParent(root.transform, false);
            turretPivot.localPosition = new Vector3(0f, ht, turretZ);

            var turret = new MeshKit();
            Vector2[] footprint = arty
                ? new[] { new Vector2(-tw * 0.45f, tl * 0.5f), new Vector2(tw * 0.45f, tl * 0.5f), new Vector2(tw * 0.5f, -tl * 0.5f), new Vector2(-tw * 0.5f, -tl * 0.5f) }
                : new[]
                {
                    new Vector2(-tw * 0.3f, tl * 0.5f), new Vector2(tw * 0.3f, tl * 0.5f),
                    new Vector2(tw * 0.5f, tl * 0.12f), new Vector2(tw * 0.48f, -tl * 0.5f),
                    new Vector2(-tw * 0.48f, -tl * 0.5f), new Vector2(-tw * 0.5f, tl * 0.12f),
                };
            turret.Add(Shapes.Frustum(footprint, th, arty ? 0.93f : 0.84f, new Vector2(0f, -tl * 0.03f)), m.body, Vector3.zero);
            turret.CylinderY(m.steel, new Vector3(0f, 0.03f, 0f), tw * 0.42f, 0.1f); // taret yatağı

            // Ön yanaklarda ek zırh blokları (modern tanklar)
            if (!arty && !light)
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int row = 0; row < 2; row++)
                        for (int cc = 0; cc < 2; cc++)
                        {
                            Vector3 c = new Vector3(side * (tw * 0.28f + cc * tw * 0.1f), th * (0.3f + row * 0.32f), tl * 0.47f - cc * tl * 0.12f);
                            turret.Box(m.body, c, new Vector3(tw * 0.09f, th * 0.28f, 0.14f), Quaternion.Euler(0f, side * 28f, 0f));
                        }
            }

            // Arka depo sepeti: kafes + çantalar
            float bz = -tl * 0.5f - tl * 0.14f;
            turret.Box(m.steel, new Vector3(0f, th * 0.18f, bz), new Vector3(tw * 0.86f, 0.04f, tl * 0.28f));
            for (int side = -1; side <= 1; side += 2)
                turret.Box(m.steel, new Vector3(side * tw * 0.43f, th * 0.4f, bz), new Vector3(0.03f, th * 0.45f, tl * 0.28f));
            turret.Box(m.steel, new Vector3(0f, th * 0.4f, bz - tl * 0.14f), new Vector3(tw * 0.86f, th * 0.45f, 0.03f));
            turret.Box(m.canvas, new Vector3(-tw * 0.18f, th * 0.36f, bz), new Vector3(tw * 0.34f, th * 0.34f, tl * 0.22f));
            turret.Capsule(m.canvas, new Vector3(tw * 0.2f, th * 0.34f, bz), new Vector3(th * 0.3f, tw * 0.17f, th * 0.3f), Quaternion.Euler(0f, 0f, 90f));

            // Nişancı nişangahı (camlı kutu)
            Vector3 sight = new Vector3(-tw * 0.26f, th + 0.12f, tl * 0.22f);
            turret.Box(m.body, sight, new Vector3(0.36f, 0.26f, 0.42f));
            turret.Box(m.glass, sight + new Vector3(0f, 0.02f, 0.22f), new Vector3(0.26f, 0.14f, 0.02f));

            // Komutan kubbesi, periskoplar, kapak ve makineli tüfek
            Vector3 cupola = new Vector3(tw * 0.2f, th, -tl * 0.12f);
            turret.CylinderY(m.body, cupola + Vector3.up * 0.13f, 0.27f, 0.26f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 off = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.27f;
                turret.Box(m.glass, cupola + Vector3.up * 0.2f + off, new Vector3(0.1f, 0.06f, 0.1f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f));
            }
            turret.CylinderY(m.steel, cupola + Vector3.up * 0.28f, 0.29f, 0.05f);
            turret.CylinderZ(m.grille, cupola + new Vector3(0f, 0.4f, 0.35f), 0.03f, 0.9f);            // namlu
            turret.Box(m.steel, cupola + new Vector3(0f, 0.38f, 0.02f), new Vector3(0.09f, 0.12f, 0.35f)); // gövde
            turret.Box(m.steel, cupola + new Vector3(0f, 0.36f, 0.1f), new Vector3(0.08f, 0.08f, 0.1f)); // şarjör kutusu
            turret.Box(m.steel, cupola + new Vector3(0f, 0.36f, 0.22f), new Vector3(0.3f, 0.2f, 0.02f)); // kalkan

            // Yükleyici kapağı ve kaldırma kancaları
            turret.CylinderY(m.steel, new Vector3(-tw * 0.2f, th + 0.02f, -tl * 0.12f), 0.22f, 0.05f);
            for (int side = -1; side <= 1; side += 2)
                turret.Box(m.steel, new Vector3(side * tw * 0.36f, th + 0.05f, tl * 0.05f), new Vector3(0.04f, 0.1f, 0.14f));

            // Rüzgar sensörü direği (arka tavan)
            turret.CylinderY(m.steel, new Vector3(tw * 0.02f, th + 0.3f, -tl * 0.42f), 0.02f, 0.6f);
            turret.Box(m.steel, new Vector3(tw * 0.02f, th + 0.6f, -tl * 0.42f), new Vector3(0.12f, 0.03f, 0.03f));

            // Sis bombası atıcıları
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                    turret.Cylinder(m.steel, new Vector3(side * (tw * 0.44f), th * 0.7f, tl * 0.2f + i * 0.14f), 0.05f, 0.22f,
                                    Quaternion.Euler(-60f, side * 30f, 0f));

            // Takım şeritleri (taretin iki yanında) ve tepe işareti
            for (int side = -1; side <= 1; side += 2)
                turret.Box(m.marker, new Vector3(side * (tw * 0.465f + 0.02f), th * 0.48f, -tl * 0.12f),
                           new Vector3(0.03f, th * 0.2f, tl * 0.5f), Quaternion.Euler(0f, 0f, side * -6f));

            turret.Build($"Tank_{s.id}_Turret", "TaretGövde", turretPivot, Vector3.zero, Quaternion.identity);

            // Anten (ayrı parça: hareketle sallanır) ve takım bayrağı
            var antennaPivot = new GameObject("Anten").transform;
            antennaPivot.SetParent(turretPivot, false);
            antennaPivot.localPosition = new Vector3(-tw * 0.34f, th, -tl * 0.38f);
            var antenna = new MeshKit();
            antenna.CylinderY(m.steel, new Vector3(0f, 0.08f, 0f), 0.06f, 0.16f);
            antenna.CylinderY(m.steel, new Vector3(0f, 1.1f, 0f), 0.015f, 2.1f);
            antenna.Box(m.marker, new Vector3(0f, 1.95f, -0.17f), new Vector3(0.02f, 0.2f, 0.32f));
            antenna.Build($"Tank_{s.id}_Antenna", "AntenMesh", antennaPivot, Vector3.zero, Quaternion.identity);

            // ---------------------------------------------------------------- Namlu
            var gunPivot = new GameObject("Gun").transform;
            gunPivot.SetParent(turretPivot, false);
            gunPivot.localPosition = new Vector3(0f, th * 0.5f, tl * 0.5f - 0.1f);

            var mantlet = new MeshKit();
            mantlet.Box(m.body, new Vector3(0f, 0f, 0.12f), new Vector3(tw * 0.38f, th * 0.62f, 0.42f));
            mantlet.Box(m.body, new Vector3(0f, 0f, 0.38f), new Vector3(tw * 0.22f, th * 0.42f, 0.2f));
            mantlet.Box(m.canvas, new Vector3(0f, 0f, 0.52f), new Vector3(tw * 0.2f, th * 0.36f, 0.12f)); // toz brandası
            mantlet.CylinderZ(m.grille, new Vector3(tw * 0.14f, -th * 0.1f, 0.5f), 0.03f, 0.3f);          // eş eksenli makineli
            mantlet.Build($"Tank_{s.id}_Mantlet", "Kalkan", gunPivot, Vector3.zero, Quaternion.identity);

            var recoil = new GameObject("GeriTepme").transform;
            recoil.SetParent(gunPivot, false);

            float bl = s.barrelLength, r = s.barrelRadius;
            var barrel = new MeshKit();
            barrel.CylinderZ(m.body, new Vector3(0f, 0f, 0.4f + bl * 0.25f), r * 1.35f, bl * 0.5f); // kalın dip kısmı
            barrel.CylinderZ(m.body, new Vector3(0f, 0f, 0.4f + bl * 0.5f), r, bl);
            barrel.CylinderZ(m.body, new Vector3(0f, 0f, 0.4f + bl * 0.62f), r * 1.5f, bl * 0.12f); // duman tahliyesi
            // Isı kılıfı bantları
            for (int i = 0; i < 4; i++)
                barrel.CylinderZ(m.steel, new Vector3(0f, 0f, 0.4f + bl * (0.3f + i * 0.14f)), r * 1.08f, 0.04f);
            barrel.CylinderZ(m.grille, new Vector3(0f, 0f, 0.4f + bl + 0.005f), r * 0.7f, 0.02f); // namlu ağzı (koyu)
            if (heavy || arty)
            {
                barrel.Box(m.steel, new Vector3(0f, 0f, 0.4f + bl - bl * 0.04f), new Vector3(r * 3.6f, r * 2.3f, Mathf.Max(0.25f, bl * 0.08f)));
                barrel.CylinderZ(m.steel, new Vector3(0f, 0f, 0.4f + bl + 0.02f), r * 1.1f, 0.1f);
            }
            else if (!light)
            {
                barrel.CylinderZ(m.steel, new Vector3(0f, 0f, 0.4f + bl - 0.12f), r * 1.3f, 0.28f);
                barrel.Box(m.steel, new Vector3(0f, r * 1.3f, 0.4f + bl - 0.05f), new Vector3(0.06f, 0.05f, 0.1f)); // namlu referans sensörü
            }
            else
            {
                barrel.CylinderZ(m.steel, new Vector3(0f, 0f, 0.4f + bl - 0.05f), r * 1.15f, 0.12f);
            }
            barrel.Build($"Tank_{s.id}_Barrel", "Namlu", recoil, Vector3.zero, Quaternion.identity);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gunPivot, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.4f + bl + 0.35f);

            // ---------------------------------------------------------------- Toz
            var dust = CreateDust(root.transform, new Vector3(0f, 0.15f, -L / 2f), hullW, m.dust);

            // ---------------------------------------------------------------- Fizik ve bileşenler
            float total = ht + th;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (0.05f + total) / 2f, 0f);
            col.size = new Vector3(hullW, total - 0.05f, L);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = s.mass;
            rb.linearDamping = 0f;
            rb.angularDamping = 2f;

            var tank = root.AddComponent<TankController>();
            tank.turret = turretPivot;
            tank.gun = gunPivot;
            tank.muzzle = muzzle;
            tank.teamColorRenderers = new Renderer[0];
            tank.muzzleFlashPrefab = flash;
            tank.deathEffectPrefab = explosion;

            var visuals = root.AddComponent<TankVisuals>();
            visuals.leftWheels = leftWheels.ToArray();
            visuals.rightWheels = rightWheels.ToArray();
            visuals.wheelRadius = wheelR;
            visuals.trackHalfWidth = trackX;
            visuals.recoilPart = recoil;
            visuals.recoilDistance = light ? 0.25f : heavy ? 0.5f : arty ? 0.7f : 0.4f;
            visuals.dust = dust;
            visuals.antenna = antennaPivot;

            root.AddComponent<BotInputSource>();

            // Gölge: küçük parçalar gölge düşürmesin (mobil performans)
            foreach (var rend in wheelRoot.GetComponentsInChildren<Renderer>())
                rend.shadowCastingMode = ShadowCastingMode.Off;

            string path = $"{Root}/Prefabs/Tank_{s.id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static ParticleSystem CreateDust(Transform parent, Vector3 localPos, float width, Material mat)
        {
            var go = new GameObject("Toz");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startColor = new Color(0.55f, 0.5f, 0.44f, 0.22f);
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 0.2f, 0.5f);

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = fade;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }
    }
}
