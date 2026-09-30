using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MiniTank.EditorTools
{
    /// <summary>
    /// Arenaları dolduran detaylı objeler: pencereli binalar, kum torbaları, palmiyeler,
    /// katmanlı çamlar, kayalar, oluklu konteynerler, vinçler, depolar, bacalar, boru hatları.
    /// Her obje birleştirilmiş tek mesh + basit kutu çarpıştırıcılardan oluşur.
    /// </summary>
    public static partial class MiniTankBuilder
    {
        static int propCounter;

        /// <summary>Bir kiti sahneye koyar ve verilen kutu çarpıştırıcılarını ekler.</summary>
        static GameObject PlaceKit(ArenaContext c, MeshKit kit, string meshName, string objectName,
                                   Vector3 pos, float rotY, params Bounds[] colliders)
        {
            var go = kit.Build($"{c.def.sceneName}_{meshName}_{propCounter++}", objectName, c.parent, pos, Quaternion.Euler(0f, rotY, 0f));
            foreach (var b in colliders)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = b.center;
                col.size = b.size;
            }
            go.isStatic = true;
            return go;
        }

        static Bounds B(Vector3 center, Vector3 size) => new Bounds(center, size);

        // ================================================================ Genel

        /// <summary>Pencereli, çatı korkuluklu bina.</summary>
        static void Building(ArenaContext c, Vector3 pos, Vector3 size, float rotY, Material wall, Material trim, Material window, bool industrial = false)
        {
            var k = new MeshKit();
            k.Box(wall, new Vector3(0f, size.y / 2f, 0f), size);

            // Çatı korkuluğu
            float p = 0.45f, t = 0.25f;
            k.Box(trim, new Vector3(0f, size.y + p / 2f, size.z / 2f - t / 2f), new Vector3(size.x, p, t));
            k.Box(trim, new Vector3(0f, size.y + p / 2f, -size.z / 2f + t / 2f), new Vector3(size.x, p, t));
            k.Box(trim, new Vector3(size.x / 2f - t / 2f, size.y + p / 2f, 0f), new Vector3(t, p, size.z));
            k.Box(trim, new Vector3(-size.x / 2f + t / 2f, size.y + p / 2f, 0f), new Vector3(t, p, size.z));
            // Kat arası kuşak
            int floors = Mathf.Max(1, Mathf.FloorToInt(size.y / 3f));
            for (int f = 1; f < floors; f++)
                k.Box(trim, new Vector3(0f, f * 3f, 0f), new Vector3(size.x + 0.1f, 0.15f, size.z + 0.1f));

            // Pencereler: dört cephe
            AddWindows(k, window, trim, size, floors, industrial);

            // Kapı
            k.Box(window, new Vector3(0f, 1.1f, size.z / 2f + 0.03f), new Vector3(1.3f, 2.2f, 0.08f));
            k.Box(trim, new Vector3(0f, 2.3f, size.z / 2f + 0.12f), new Vector3(1.8f, 0.12f, 0.3f));

            // Çatı detayları
            float r = c.Range(0f, 1f);
            if (r < 0.4f)
            {
                k.CylinderY(trim, new Vector3(size.x * 0.2f, size.y + 0.9f, -size.z * 0.15f), 0.7f, 1.4f);
                k.Box(trim, new Vector3(size.x * 0.2f, size.y + 0.3f, -size.z * 0.15f), new Vector3(1.2f, 0.6f, 1.2f));
            }
            else if (r < 0.8f)
            {
                k.Box(trim, new Vector3(-size.x * 0.2f, size.y + 0.35f, size.z * 0.1f), new Vector3(1.1f, 0.7f, 0.8f));
                k.Box(trim, new Vector3(size.x * 0.15f, size.y + 0.25f, size.z * 0.2f), new Vector3(0.8f, 0.5f, 0.6f));
            }

            PlaceKit(c, k, "Bina", industrial ? "Fabrika Binası" : "Bina", pos, rotY,
                     B(new Vector3(0f, size.y / 2f, 0f), size));
        }

        static void AddWindows(MeshKit k, Material window, Material trim, Vector3 size, int floors, bool industrial)
        {
            float ww = industrial ? 2.2f : 1.0f, wh = industrial ? 1.0f : 1.2f;
            for (int f = 0; f < floors; f++)
            {
                float y = f * 3f + (industrial ? 2.2f : 1.7f);
                // Ön ve arka (Z yönündeki yüzler)
                int nx = Mathf.Max(1, Mathf.FloorToInt((size.x - 1.5f) / (ww + 1.4f)));
                for (int i = 0; i < nx; i++)
                {
                    float x = (i - (nx - 1) / 2f) * (size.x - 1.5f) / Mathf.Max(1, nx);
                    if (f == 0 && Mathf.Abs(x) < 1.3f) continue; // kapı boşluğu
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        float z = sd * (size.z / 2f + 0.02f);
                        k.Box(window, new Vector3(x, y, z), new Vector3(ww, wh, 0.06f));
                        k.Box(trim, new Vector3(x, y - wh / 2f - 0.06f, z + sd * 0.05f), new Vector3(ww + 0.2f, 0.1f, 0.14f));
                    }
                }
                // Yanlar (X yönündeki yüzler)
                int nz = Mathf.Max(1, Mathf.FloorToInt((size.z - 1.5f) / (ww + 1.4f)));
                for (int i = 0; i < nz; i++)
                {
                    float z = (i - (nz - 1) / 2f) * (size.z - 1.5f) / Mathf.Max(1, nz);
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        float x = sd * (size.x / 2f + 0.02f);
                        k.Box(window, new Vector3(x, y, z), new Vector3(0.06f, wh, ww));
                        k.Box(trim, new Vector3(x + sd * 0.05f, y - wh / 2f - 0.06f, z), new Vector3(0.14f, 0.1f, ww + 0.2f));
                    }
                }
            }
        }

        /// <summary>Beton bariyer (Jersey bariyeri kesiti).</summary>
        static void ConcreteBarrier(ArenaContext c, Vector3 pos, float length, float rotY, Material concrete)
        {
            var k = new MeshKit();
            var profile = new[]
            {
                new Vector2(-0.3f, 0f), new Vector2(0.3f, 0f), new Vector2(0.18f, 0.3f),
                new Vector2(0.1f, 1.0f), new Vector2(-0.1f, 1.0f), new Vector2(-0.18f, 0.3f),
            };
            // Prism X boyunca uzanır; profil (z, y) düzleminde
            var mesh = Shapes.Prism(profile, 2f);
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 2.05f));
            for (int i = 0; i < n; i++)
                k.Add(mesh, concrete, new Vector3((i - (n - 1) / 2f) * 2.05f, 0f, 0f));
            PlaceKit(c, k, "Bariyer", "Beton Bariyer", pos, rotY, B(new Vector3(0f, 0.5f, 0f), new Vector3(n * 2.05f, 1f, 0.6f)));
        }

        /// <summary>Kum torbası siperi.</summary>
        static void SandbagWall(ArenaContext c, Vector3 pos, float length, float rotY, Material bag)
        {
            var k = new MeshKit();
            int perRow = Mathf.Max(2, Mathf.RoundToInt(length / 0.85f));
            for (int row = 0; row < 3; row++)
            {
                float offset = row % 2 == 0 ? 0f : 0.42f;
                int count = perRow - (row % 2);
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (perRow - 1) / 2f) * 0.85f + offset;
                    k.Capsule(bag, new Vector3(x, 0.16f + row * 0.28f, 0f),
                              new Vector3(0.32f, 0.44f, 0.52f), Quaternion.Euler(0f, 0f, 90f));
                }
            }
            PlaceKit(c, k, "KumTorbasi", "Kum Torbası Siperi", pos, rotY,
                     B(new Vector3(0f, 0.45f, 0f), new Vector3(perRow * 0.85f, 0.9f, 0.55f)));
        }

        /// <summary>Tahta sandık yığını.</summary>
        static void CrateStack(ArenaContext c, Vector3 pos, float rotY, Material wood, Material frame)
        {
            var k = new MeshKit();
            AddCrate(k, wood, frame, Vector3.zero, 1.3f);
            AddCrate(k, wood, frame, new Vector3(1.4f, 0f, 0.2f), 1.2f);
            AddCrate(k, wood, frame, new Vector3(0.6f, 1.3f, 0.1f), 1.1f);
            PlaceKit(c, k, "Sandik", "Sandıklar", pos, rotY, B(new Vector3(0.7f, 1.2f, 0.1f), new Vector3(2.8f, 2.4f, 1.4f)));
        }

        static void AddCrate(MeshKit k, Material wood, Material frame, Vector3 p, float s)
        {
            k.Box(wood, p + Vector3.up * s / 2f, Vector3.one * s);
            // Kenar çıtaları
            k.Box(frame, p + new Vector3(0f, s / 2f, s / 2f + 0.01f), new Vector3(s * 1.02f, s * 0.12f, 0.04f));
            k.Box(frame, p + new Vector3(0f, s / 2f, -s / 2f - 0.01f), new Vector3(s * 1.02f, s * 0.12f, 0.04f));
            k.Box(frame, p + new Vector3(s / 2f + 0.01f, s / 2f, 0f), new Vector3(0.04f, s * 0.12f, s * 1.02f));
            k.Box(frame, p + new Vector3(-s / 2f - 0.01f, s / 2f, 0f), new Vector3(0.04f, s * 0.12f, s * 1.02f));
        }

        /// <summary>Yanmış araç enkazı.</summary>
        static void CarWreck(ArenaContext c, Vector3 pos, float rotY, Material burnt, Material dark)
        {
            var k = new MeshKit();
            k.Box(burnt, new Vector3(0f, 0.7f, 0f), new Vector3(1.9f, 0.7f, 4.3f));
            k.Box(burnt, new Vector3(0f, 1.3f, -0.3f), new Vector3(1.7f, 0.6f, 2.1f), Quaternion.Euler(0f, 0f, 4f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.CylinderX(dark, new Vector3(sx * 0.9f, 0.35f, sz * 1.35f), 0.35f, 0.25f);
            PlaceKit(c, k, "Enkaz", "Araç Enkazı", pos, rotY, B(new Vector3(0f, 0.8f, 0f), new Vector3(2f, 1.6f, 4.4f)));
        }

        static Mesh[] palmMeshes;
        static Material[] palmMats;

        /// <summary>3 farklı palmiye modeli hazırlar.</summary>
        static void PreparePalms(Material trunk, Material leaf)
        {
            var rng = new System.Random(3);
            palmMeshes = new Mesh[3];
            for (int v = 0; v < palmMeshes.Length; v++)
            {
                var k = new MeshKit();
                float h = 6f + v;
                float lean = (v - 1) * 7f;
                Vector3 top = Vector3.zero;
                int segs = 5;
                for (int i = 0; i < segs; i++)
                {
                    float t = (i + 0.5f) / segs;
                    Vector3 p = new Vector3(Mathf.Sin(lean * Mathf.Deg2Rad) * h * t * t, h * t, 0f);
                    k.Cylinder(trunk, p, Mathf.Lerp(0.28f, 0.18f, t), h / segs * 1.05f, Quaternion.Euler(0f, 0f, -lean * t * 1.5f));
                    top = p + Vector3.up * h / segs / 2f;
                }
                int fronds = 8;
                for (int i = 0; i < fronds; i++)
                {
                    float yaw = i * 360f / fronds + (float)rng.NextDouble() * 20f - 10f;
                    Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(20f + (float)rng.NextDouble() * 20f, 0f, 0f);
                    k.Box(leaf, top + q * new Vector3(0f, 0f, 1.5f), new Vector3(0.7f, 0.05f, 3.2f), q);
                }
                for (int i = 0; i < 3; i++)
                    k.Sphere(trunk, top + Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0.25f, -0.25f, 0f), Vector3.one * 0.3f);
                palmMeshes[v] = k.BuildMesh("Shape_Palm" + v, out palmMats);
            }
        }

        static void Palm(ArenaContext c, Vector3 pos)
        {
            var go = MeshKit.Spawn(palmMeshes[c.rng.Next(palmMeshes.Length)], palmMats, "Palmiye", c.parent, pos,
                                   Quaternion.Euler(0f, c.Range(0f, 360f), 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 3f, 0f);
            col.size = new Vector3(0.7f, 6f, 0.7f);
            go.isStatic = true;
        }

        // ================================================================ Kar / orman

        static Mesh coneMesh, snowCapMesh;
        static Mesh[] rockMeshes;

        static void PrepareShapes()
        {
            coneMesh = MeshKit.SaveMesh(Shapes.Cone(1f, 1f, 9), "Shape_Cone");
            snowCapMesh = MeshKit.SaveMesh(Shapes.Cone(1f, 1f, 9), "Shape_SnowCap");
            rockMeshes = new Mesh[4];
            for (int i = 0; i < rockMeshes.Length; i++)
                rockMeshes[i] = MeshKit.SaveMesh(Shapes.Rock(100 + i * 17), "Shape_Rock" + i);
        }

        static Mesh[] pineMeshes;
        static Material[] pineMats;

        /// <summary>4 farklı katmanlı, karlı çam modeli hazırlar.</summary>
        static void PreparePines(Material trunk, Material needles, Material snow)
        {
            var rng = new System.Random(7);
            pineMeshes = new Mesh[4];
            for (int v = 0; v < pineMeshes.Length; v++)
            {
                var k = new MeshKit();
                float h = 8f + v * 0.8f;
                float w = 2.7f + (float)rng.NextDouble() * 0.7f;
                k.CylinderY(trunk, new Vector3(0f, h * 0.2f, 0f), 0.3f, h * 0.4f);
                int tiers = 3 + v % 2 + 1;
                for (int i = 0; i < tiers; i++)
                {
                    float t = (float)i / tiers;
                    float radius = Mathf.Lerp(w, w * 0.35f, t);
                    float baseY = h * (0.2f + t * 0.62f);
                    float tierH = h * 0.36f;
                    k.Add(coneMesh, needles, new Vector3(0f, baseY, 0f), Quaternion.Euler(0f, i * 20f, 0f), new Vector3(radius, tierH, radius));
                    k.Add(snowCapMesh, snow, new Vector3(0f, baseY + tierH * 0.45f, 0f), Quaternion.Euler(0f, i * 20f + 10f, 0f),
                          new Vector3(radius * 0.58f, tierH * 0.56f, radius * 0.58f));
                }
                pineMeshes[v] = k.BuildMesh("Shape_Pine" + v, out pineMats);
            }
        }

        static void Pine(ArenaContext c, Vector3 pos)
        {
            var mesh = pineMeshes[c.rng.Next(pineMeshes.Length)];
            float s = c.Range(0.85f, 1.25f);
            var go = MeshKit.Spawn(mesh, pineMats, "Çam", c.parent, pos, Quaternion.Euler(0f, c.Range(0f, 360f), 0f));
            go.transform.localScale = Vector3.one * s;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 2.5f, 0f);
            col.size = new Vector3(0.9f, 5f, 0.9f);
            go.isStatic = true;
        }

        static void Rock(ArenaContext c, Vector3 pos, float scale, Material rock, Material snow)
        {
            var mesh = rockMeshes[c.rng.Next(rockMeshes.Length)];
            var go = MeshKit.Spawn(mesh, new[] { rock }, "Kaya", c.parent, pos + Vector3.up * scale * 0.25f,
                                   Quaternion.Euler(c.Range(-10f, 10f), c.Range(0f, 360f), c.Range(-10f, 10f)));
            go.transform.localScale = new Vector3(scale * c.Range(1f, 1.5f), scale * c.Range(0.6f, 0.9f), scale * c.Range(1f, 1.4f));
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
            go.isStatic = true;

            if (snow != null)
            {
                var cap = MeshKit.Spawn(mesh, new[] { snow }, "Kar", go.transform, new Vector3(0f, 0.18f, 0f), Quaternion.identity);
                cap.transform.localScale = new Vector3(0.85f, 0.8f, 0.85f);
                cap.isStatic = true;
            }
        }

        static void Log(ArenaContext c, Vector3 pos, float rotY, Material bark, Material cut)
        {
            var k = new MeshKit();
            float len = c.Range(5f, 8f);
            k.CylinderX(bark, new Vector3(0f, 0.45f, 0f), 0.45f, len);
            k.CylinderX(cut, new Vector3(len / 2f + 0.01f, 0.45f, 0f), 0.4f, 0.03f);
            k.CylinderX(cut, new Vector3(-len / 2f - 0.01f, 0.45f, 0f), 0.4f, 0.03f);
            PlaceKit(c, k, "Kutuk", "Kütük", pos, rotY, B(new Vector3(0f, 0.45f, 0f), new Vector3(len, 0.9f, 0.9f)));
        }

        /// <summary>Beşik çatılı ahşap kulübe.</summary>
        static void Cabin(ArenaContext c, Vector3 pos, float rotY, Material wood, Material roof, Material window)
        {
            var k = new MeshKit();
            Vector3 size = new Vector3(6f, 3f, 5f);
            k.Box(wood, new Vector3(0f, size.y / 2f, 0f), size);
            // Kütük dokusu: yatay çıtalar
            for (float y = 0.3f; y < size.y; y += 0.5f)
            {
                k.Box(wood, new Vector3(0f, y, size.z / 2f + 0.05f), new Vector3(size.x + 0.3f, 0.12f, 0.1f));
                k.Box(wood, new Vector3(0f, y, -size.z / 2f - 0.05f), new Vector3(size.x + 0.3f, 0.12f, 0.1f));
            }
            var roofProfile = new[] { new Vector2(-size.z / 2f - 0.6f, 0f), new Vector2(size.z / 2f + 0.6f, 0f), new Vector2(0f, 2.2f) };
            k.Add(Shapes.Prism(roofProfile, size.x + 0.8f), roof, new Vector3(0f, size.y, 0f));
            k.Box(window, new Vector3(1.6f, 1.6f, size.z / 2f + 0.11f), new Vector3(1f, 1f, 0.05f));
            k.Box(window, new Vector3(-1.2f, 1.1f, size.z / 2f + 0.11f), new Vector3(1.1f, 2.1f, 0.05f));
            k.Box(roof, new Vector3(size.x * 0.3f, size.y + 2.2f, -0.6f), new Vector3(0.6f, 1.6f, 0.6f)); // baca
            PlaceKit(c, k, "Kulube", "Kulübe", pos, rotY, B(new Vector3(0f, 2.2f, 0f), new Vector3(size.x, 4.4f, size.z)));
        }

        // ================================================================ Liman

        static Mesh containerMesh;
        static Material[] containerSlots;

        /// <summary>Oluklu konteyner mesh'i: [0] boya, [1] çerçeve.</summary>
        static void PrepareContainer(Material paintSlot, Material frameSlot)
        {
            var k = new MeshKit();
            Vector3 size = new Vector3(2.44f, 2.6f, 12f);
            k.Box(paintSlot, new Vector3(0f, size.y / 2f, 0f), size - new Vector3(0.1f, 0.1f, 0.1f));
            // Yan oluklar
            for (float z = -size.z / 2f + 0.4f; z < size.z / 2f - 0.3f; z += 0.45f)
                for (int sd = -1; sd <= 1; sd += 2)
                    k.Box(paintSlot, new Vector3(sd * (size.x / 2f - 0.02f), size.y / 2f, z), new Vector3(0.06f, size.y - 0.3f, 0.2f));
            // Köşe direkleri ve raylar
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.Box(frameSlot, new Vector3(sx * (size.x / 2f - 0.08f), size.y / 2f, sz * (size.z / 2f - 0.08f)), new Vector3(0.18f, size.y, 0.18f));
            for (int sy = 0; sy <= 1; sy++)
                for (int sx = -1; sx <= 1; sx += 2)
                    k.Box(frameSlot, new Vector3(sx * (size.x / 2f - 0.06f), 0.08f + sy * (size.y - 0.16f), 0f), new Vector3(0.14f, 0.16f, size.z));
            // Kapı kilit çubukları
            for (int i = 0; i < 4; i++)
                k.Box(frameSlot, new Vector3(-0.9f + i * 0.6f, size.y / 2f, size.z / 2f + 0.02f), new Vector3(0.05f, size.y - 0.4f, 0.05f));
            containerMesh = k.BuildMesh("Shape_Container", out containerSlots);
        }

        static void Container(ArenaContext c, Vector3 pos, bool alongZ, Material paint, Material frame)
        {
            var go = MeshKit.Spawn(containerMesh, new[] { paint, frame }, "Konteyner", c.parent, pos,
                                   Quaternion.Euler(0f, alongZ ? 0f : 90f, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.3f, 0f);
            col.size = new Vector3(2.44f, 2.6f, 12f);
            go.isStatic = true;
        }

        /// <summary>Liman vinci: dört bacak, köprü, bom, kabin.</summary>
        static void Crane(ArenaContext c, Vector3 pos, Material steel, Material dark, Material glass)
        {
            var k = new MeshKit();
            float h = 12f, sx = 5f, sz = 4f;
            var cols = new List<Bounds>();
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    k.Box(steel, new Vector3(x * sx, h / 2f, z * sz), new Vector3(1f, h, 1f));
                    k.Box(dark, new Vector3(x * sx, 0.4f, z * sz), new Vector3(1.6f, 0.8f, 1.8f)); // tekerlek bojisi
                    cols.Add(B(new Vector3(x * sx, h / 2f, z * sz), new Vector3(1.6f, h, 1.8f)));
                }
            // Çapraz destekler
            for (int x = -1; x <= 1; x += 2)
            {
                float ang = Mathf.Atan2(h * 0.6f, sz * 2f) * Mathf.Rad2Deg;
                k.Box(steel, new Vector3(x * sx, h * 0.45f, 0f), new Vector3(0.35f, 0.35f, Mathf.Sqrt(sz * sz * 4f + h * h * 0.36f)), Quaternion.Euler(-ang, 0f, 0f));
            }
            k.Box(steel, new Vector3(0f, h, 0f), new Vector3(sx * 2f + 1.5f, 1.2f, sz * 2f + 1f));
            k.Box(steel, new Vector3(0f, h + 1.1f, 6f), new Vector3(1.4f, 1f, 30f)); // bom
            k.Box(dark, new Vector3(0f, h - 0.9f, 2f), new Vector3(2f, 1.6f, 2.2f)); // kabin
            k.Box(glass, new Vector3(0f, h - 0.8f, 3.12f), new Vector3(1.7f, 0.9f, 0.05f));
            k.CylinderY(dark, new Vector3(0f, h - 4f, 12f), 0.04f, 8f); // halat
            k.Box(dark, new Vector3(0f, h - 8.2f, 12f), new Vector3(1.4f, 0.5f, 2.5f)); // spreader
            PlaceKit(c, k, "Vinc", "Liman Vinci", pos, 0f, cols.ToArray());
        }

        /// <summary>Beşik çatılı, oluklu sac depo.</summary>
        static void Warehouse(ArenaContext c, Vector3 pos, Vector3 size, float rotY, Material wall, Material roof, Material door)
        {
            var k = new MeshKit();
            k.Box(wall, new Vector3(0f, size.y / 2f, 0f), size);
            for (float x = -size.x / 2f + 0.5f; x < size.x / 2f; x += 0.6f)
                for (int sd = -1; sd <= 1; sd += 2)
                    k.Box(wall, new Vector3(x, size.y / 2f, sd * (size.z / 2f + 0.03f)), new Vector3(0.18f, size.y - 0.2f, 0.06f));
            var roofProfile = new[] { new Vector2(-size.z / 2f - 0.5f, 0f), new Vector2(size.z / 2f + 0.5f, 0f), new Vector2(0f, size.z * 0.22f) };
            k.Add(Shapes.Prism(roofProfile, size.x + 0.6f), roof, new Vector3(0f, size.y, 0f));
            k.Box(door, new Vector3(size.x / 2f + 0.04f, size.y * 0.35f, 0f), new Vector3(0.08f, size.y * 0.7f, size.z * 0.4f));
            k.Box(door, new Vector3(-size.x / 2f - 0.04f, size.y * 0.35f, 0f), new Vector3(0.08f, size.y * 0.7f, size.z * 0.4f));
            PlaceKit(c, k, "Depo", "Depo", pos, rotY, B(new Vector3(0f, size.y / 2f, 0f), size));
        }

        static void Bollard(ArenaContext c, Vector3 pos, Material dark)
        {
            var k = new MeshKit();
            k.CylinderY(dark, new Vector3(0f, 0.35f, 0f), 0.25f, 0.7f);
            k.CylinderY(dark, new Vector3(0f, 0.72f, 0f), 0.32f, 0.08f);
            PlaceKit(c, k, "Baba", "Babalık", pos, 0f);
        }

        // ================================================================ Fabrika

        static void Chimney(ArenaContext c, Vector3 pos, float height, Material concrete, Material red, Material white)
        {
            var k = new MeshKit();
            k.CylinderY(concrete, new Vector3(0f, 1f, 0f), 2.2f, 2f); // kaide
            int bands = 6;
            for (int i = 0; i < bands; i++)
            {
                float y0 = 2f + (height - 2f) * i / bands;
                float seg = (height - 2f) / bands;
                float rad = Mathf.Lerp(1.5f, 1.1f, (float)i / bands);
                k.CylinderY(i % 2 == 0 ? red : white, new Vector3(0f, y0 + seg / 2f, 0f), rad, seg);
            }
            k.CylinderY(concrete, new Vector3(0f, height + 0.2f, 0f), 1.2f, 0.4f);
            PlaceKit(c, k, "Baca", "Baca", pos, 0f, B(new Vector3(0f, height / 2f, 0f), new Vector3(3.2f, height, 3.2f)));
        }

        /// <summary>Kubbeli depolama tankı ve merdiveni.</summary>
        static void StorageTank(ArenaContext c, Vector3 pos, float radius, float height, Material metal, Material dark)
        {
            var k = new MeshKit();
            k.CylinderY(metal, new Vector3(0f, height / 2f, 0f), radius, height);
            k.Sphere(metal, new Vector3(0f, height, 0f), new Vector3(radius * 2f, radius * 0.7f, radius * 2f));
            for (int i = 1; i < 4; i++)
                k.CylinderY(dark, new Vector3(0f, height * i / 4f, 0f), radius * 1.02f, 0.12f);
            // Merdiven
            k.Box(dark, new Vector3(radius + 0.2f, height / 2f, 0.3f), new Vector3(0.06f, height, 0.06f));
            k.Box(dark, new Vector3(radius + 0.2f, height / 2f, -0.3f), new Vector3(0.06f, height, 0.06f));
            for (float y = 0.4f; y < height; y += 0.5f)
                k.Box(dark, new Vector3(radius + 0.2f, y, 0f), new Vector3(0.05f, 0.05f, 0.6f));
            PlaceKit(c, k, "SilindirDepo", "Depolama Tankı", pos, c.Range(0f, 360f),
                     B(new Vector3(0f, height / 2f, 0f), new Vector3(radius * 1.8f, height, radius * 1.8f)));
        }

        /// <summary>Yükseltilmiş boru hattı: tanklar altından geçebilir, sadece ayaklar engeldir.</summary>
        static void PipeRack(ArenaContext c, Vector3 pos, float length, float rotY, Material pipe, Material steel)
        {
            var k = new MeshKit();
            var cols = new List<Bounds>();
            int supports = Mathf.Max(2, Mathf.RoundToInt(length / 8f) + 1);
            for (int i = 0; i < supports; i++)
            {
                float x = -length / 2f + length * i / (supports - 1);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    k.Box(steel, new Vector3(x, 2.8f, sd * 1.2f), new Vector3(0.35f, 5.6f, 0.35f));
                    cols.Add(B(new Vector3(x, 2.8f, sd * 1.2f), new Vector3(0.4f, 5.6f, 0.4f)));
                }
                k.Box(steel, new Vector3(x, 5.5f, 0f), new Vector3(0.3f, 0.3f, 2.8f));
            }
            k.CylinderX(pipe, new Vector3(0f, 5.95f, -0.6f), 0.35f, length);
            k.CylinderX(pipe, new Vector3(0f, 5.85f, 0.2f), 0.25f, length);
            k.CylinderX(steel, new Vector3(0f, 5.8f, 0.8f), 0.18f, length);
            PlaceKit(c, k, "BoruHatti", "Boru Hattı", pos, rotY, cols.ToArray());
        }

        /// <summary>Kalın beton duvar, üst başlıklı ve payandalı.</summary>
        static void ConcreteWall(ArenaContext c, Vector3 pos, Vector3 size, Material concrete, Material cap)
        {
            var k = new MeshKit();
            k.Box(concrete, new Vector3(0f, size.y / 2f, 0f), size);
            bool alongX = size.x >= size.z;
            k.Box(cap, new Vector3(0f, size.y + 0.1f, 0f), new Vector3(size.x + 0.15f, 0.2f, size.z + 0.15f));
            float len = alongX ? size.x : size.z;
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 4f) + 1);
            for (int i = 0; i < n; i++)
            {
                float t = -len / 2f + len * i / (n - 1);
                Vector3 p = alongX ? new Vector3(t, size.y / 2f, 0f) : new Vector3(0f, size.y / 2f, t);
                Vector3 s = alongX ? new Vector3(0.5f, size.y, size.z + 0.4f) : new Vector3(size.x + 0.4f, size.y, 0.5f);
                k.Box(cap, p, s);
            }
            PlaceKit(c, k, "Duvar", "Beton Duvar", pos, 0f, B(new Vector3(0f, size.y / 2f, 0f), size + new Vector3(0.4f, 0f, 0.4f)));
        }

        /// <summary>Zemin çizgisi (yol işareti vb.), çarpıştırıcısız.</summary>
        static void GroundLine(ArenaContext c, Vector3 pos, Vector3 size, float rotY, Material paint)
        {
            var k = new MeshKit();
            k.Box(paint, new Vector3(0f, 0.01f, 0f), size);
            var go = PlaceKit(c, k, "Cizgi", "Yol Çizgisi", pos, rotY);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
