# Mini Tank 5v5 – Kurulum Rehberi (1. Adım: Oynanabilir Prototip)

Bu paket, oyunun **çevrimdışı ama tam oynanabilir** ilk sürümüdür: sen + 4 bot, karşıda 5 bot, 4 arena, 2 mod, 4 tank sınıfı, süreli maç ve yeniden doğma. Online (Photon Fusion 2) bir sonraki adımda bu kodun üzerine eklenecek. Kod buna göre tasarlandı ve botlar online'da boş yerleri doldurmak için de kullanılacak.

## 1. Unity projesini hazırla

1. **Unity Hub** → Installs → **Unity 6 LTS** kur. Modüllerden şunları işaretle:
   - **Android Build Support** (OpenJDK + Android SDK & NDK dahil)
   - Visual Studio Community (kod editörü olarak)
2. Hub → New Project → **Universal 3D** şablonu → isim: `MiniTank` → Create.

## 2. Paketi projeye ekle

1. `MiniTank.zip` dosyasını aç. İçinde `Assets/MiniTank` klasörü var.
2. Bu `MiniTank` klasörünü projendeki `Assets` klasörünün içine kopyala. Sonuç şöyle olmalı: `MiniTank/Assets/MiniTank/Scripts/...`
3. Unity'ye geri dön ve derlemenin bitmesini bekle. Console'da kırmızı hata olmamalı.

## 3. Tek tıkla kurulum

Üst menüden **MiniTank → Her Şeyi Kur (Menü + 4 Arena)** seçeneğine tıkla. Araç otomatik olarak şunları oluşturur:

- 4 tank sınıfı: Hafif, Orta, Ağır, Topçu (`Assets/MiniTank/Generated/Classes`)
- Tank, mermi ve patlama efekti prefab'ları
- Ana menü sahnesi ve 4 arena sahnesi (`Assets/MiniTank/Scenes`)
- Build Settings sahne listesi ve yatay ekran ayarı

İşlem bitince ana menü sahnesi açılır. **Play** tuşuna bas, modu, tankını ve arenayı seç.

## 4. Kontroller

| | Telefon | Editör (test) |
|---|---|---|
| Hareket | Sol alttaki joystick | W A S D |
| Kamera / nişan | Ekranın boş yerinde parmak kaydır | Sağ tık basılı tut + fareyi oynat |
| Ateş | Sağ alttaki ATEŞ butonu (basılı tutulabilir) | Space |

Ekranın ortası nişangahtır. Kule, nişangahın gösterdiği yere döner. Topçuda namlu açısı hedef mesafesine göre otomatik ayarlanır.

## 5. Oyun kuralları (varsayılan)

- **Takım Ölüm Maçı:** 5 dakika, 30 öldürmeye ulaşan ya da süre bitince önde olan takım kazanır.
- **Ele Geçirme:** Noktada yalnızca senin takımının tankları varsa nokta sizin tarafa geçer. İki takım da varsa çekişmeli kalır. Tutulan her nokta saniyede 1 puan verir. 200 puana ulaşan ya da süre bitince önde olan kazanır.
- Ölen tank **5 saniye** sonra düşmandan en uzak doğma noktasında yeniden doğar.
- Dost ateşi kapalı.

## 6. Arenalar

| Arena | Boyut | Özellik | Nokta |
|---|---|---|---|
| Çöl Kasabası | 130×130 | Kerpiç evler, alçak siper duvarları | A, B, C |
| Karlı Orman | 150×150 | Sık ağaçlar, kayalar, sis | A, B, C |
| Liman | 180×120 | Konteyner sıraları, vinçler | A, B |
| Fabrika | 120×120 | Dar koridorlar, merkez bina, depolar | A, B, C |

Arenalar şimdilik "gray-box", yani oynanışı test etmek için basit geometriden oluşuyor. Oynanış oturunca görseller değiştirilecek.

## 7. Ayarları değiştirme

- **Tank dengesi:** `Generated/Classes` içindeki bir sınıfa tıkla, Inspector'dan can, hız, hasar ve dolum süresini değiştir. "Her Şeyi Kur"u tekrar çalıştırsan da bu değerlerin korunur.
- **Maç süresi, öldürme/puan hedefi, yeniden doğma süresi:** arena sahnesindeki **Maç Yöneticisi** objesinden.
- **Yeni sınıf eklemek:** Project'te sağ tık → Create → MiniTank → Tank Class. Sonra bu sınıfı Maç Yöneticisi'ndeki `Available Classes` listesine ve ana menüdeki `Classes` listesine ekle.
- **Bot zorluğu:** tank prefab'ındaki `BotInputSource` → `Aim Error Multiplier` (0 = kusursuz nişancı, 2 = acemi).

## 8. Telefonda test (Android)

1. Telefonda **Geliştirici seçenekleri → USB hata ayıklama**yı aç ve telefonu USB ile bağla.
2. Unity: **File → Build Profiles** → Android → **Switch Platform**.
3. **Build And Run**. İlk derleme birkaç dakika sürebilir.

## 9. Dosya yapısı

```
Assets/MiniTank/
├── Scripts/
│   ├── Core/     TankController, TankClassData, Projectile, ThirdPersonCamera, TankTypes
│   ├── Input/    PlayerInputSource, VirtualJoystick, TouchLookArea, FireButton
│   ├── AI/       BotInputSource
│   ├── Match/    MatchManager, CapturePoint, ArenaNavMesh
│   └── UI/       MatchHUD, MainMenu
└── Editor/       MiniTankBuilder (tek tıkla kurulum aracı)
```

## 10. Online 5v5 (Photon Fusion 2)

Online maç kurulumu, işleyişi ve testi için bölüm 15'e bak. Kod dosyaları: `Scripts/Online/` (NetMatch, OnlineSession, NetTypes).

## 11. Giriş ekranı ve Firebase

Oyun artık **Giriş** ekranıyla açılıyor. Oyuncu misafir olarak oynayabilir, e-posta ile giriş yapabilir veya hesap oluşturabilir. Misafir oyuncu, ana menüdeki **Hesabını kaydet** butonuyla misafir hesabını e-postasına bağlayabilir; bu sırada ilerlemesi korunur. Daha önce giriş yapan oyuncu, oyunu açınca doğrudan ana menüye geçer.

**Firebase kurulana kadar** giriş ekranı *cihaz içi test modunda* çalışır: hesaplar sadece o cihazda saklanır ve şifre sıfırlama e-postası gönderilmez. Ekranın sağ altında hangi modun kullanıldığı yazar.

### Firebase kurulumu (bir kerelik)

1. **Paket adını belirle:** Unity'de Edit → Project Settings → Player → Android sekmesi → Other Settings → **Package Name**. Örnek: `com.burak.minitank`. Bu adı sonra değiştirme.
2. [console.firebase.google.com](https://console.firebase.google.com) → **Proje ekle** → isim: `MiniTank`. Google Analytics isteğe bağlı.
3. Soldan **Authentication → Başlayın → Sign-in method**. Şu ikisini etkinleştir:
   - **Anonim** (misafir girişi için)
   - **E-posta/Şifre**
4. Proje ana sayfasında **Unity** simgesine tıkla → **Android uygulaması kaydet** → 1. adımdaki paket adını gir.
5. **google-services.json** dosyasını indir ve Unity projesinin **Assets** klasörüne koy.
6. [Firebase Unity SDK](https://firebase.google.com/download/unity) zip dosyasını indir ve bir klasöre çıkar.
7. Unity: **Assets → Import Package → Custom Package** → çıkardığın klasördeki **FirebaseAuth.unitypackage** dosyasını seç → **Import**.
8. İçe aktarma bitince Android bağımlılıkları kendiliğinden çözülür (sağ altta "Resolving" yazısı). Soru gelirse **Enable** de.
9. Proje Firebase'i otomatik algılar ve `MINITANK_FIREBASE` sembolünü kendisi ekler. Kontrol etmek için **MiniTank → Firebase Durumunu Kontrol Et** menüsünü kullan.

Bundan sonra giriş ekranının sağ altında "Giriş servisi: Firebase" yazar ve açılan hesaplar Firebase konsolunda **Authentication → Users** altında görünür.

Yeni sahneleri kurmak için **MiniTank → Sadece Giriş ve Menü Sahnelerini Kur** yeterli. Arenaları yeniden oluşturmana gerek yok.

## 12. Ligler ve sezonlar

Maç kazandıkça **kupa** toplanır, kupa sayısı ligi belirler:

| Lig | Kupa | Galibiyet | Yenilgi | Sezon sonunda düşülen kupa |
|---|---|---|---|---|
| Bronz | 0 – 299 | +35 | -10 | 0 (Bronz) |
| Gümüş | 300 – 699 | +32 | -15 | 150 (Bronz) |
| Altın | 700 – 1199 | +30 | -20 | 300 (Gümüş) |
| Platin | 1200 – 1799 | +28 | -22 | 700 (Altın) |
| Elmas | 1800 – 2499 | +26 | -25 | 1000 (Altın) |
| Usta | 2500+ | +25 | -30 | 1200 (Platin) |

- **Beraberlik:** +5 kupa
- **Performans bonusu:** her öldürme +1, her 2 asist +1, en fazla +8. Kaybedende bu bonus kaybı azaltır, ama en fazla yarısını.
- **MVP** (takımında en çok öldürme yapan): +5
- **3 ve üzeri galibiyet serisi:** +5
- Kupa 0'ın altına düşmez.
- **Sezon her ayın 1'inde (Türkiye saati) biter.** Tablodaki "sezon sonu" değerine düşersin; ligin ne kadar yüksekse düşüş o kadar büyük olur. Birkaç ay oynamazsan her ay için ayrı düşüş uygulanır. Yeni sezona girince ana menüde sezon özeti çıkar.
- Değerleri değiştirmek için `Scripts/League/Leagues.cs` dosyasındaki tabloyu düzenle.

### İlerlemeyi buluta taşımak (Firestore)

Firestore eklenmeden önce lig ve istatistikler sadece o cihazda saklanır. Buluta taşımak için:

1. Firebase konsolu → **Databases & Storage → Firestore Database → Create database**. Konum olarak `eur3 (europe)` seç, **production mode** ile başlat.
2. **Rules** sekmesine aşağıdaki kuralları yapıştır ve **Publish** de. Bu kurallarla giriş yapmış herkes sıralama tablosu için kayıtları okuyabilir, ama her oyuncu sadece kendi kaydını yazabilir:

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /players/{userId} {
      allow read: if request.auth != null;
      allow write: if request.auth != null && request.auth.uid == userId;
    }
  }
}
```

3. Unity: **Assets → Import Package → Custom Package** → Firebase SDK klasöründeki **FirebaseFirestore.unitypackage**.
4. Proje Firestore'u otomatik algılar (`MINITANK_FIRESTORE`). **MiniTank → Firebase Durumunu Kontrol Et** ile doğrulayabilirsin.

Oyuncu kayıtları konsolda **players** koleksiyonunda görünür. Ana menüdeki **Sıralama** ekranı bu koleksiyondaki `points` alanına göre en iyi 50 oyuncuyu listeler.

> Not: Maçlar şimdilik oyuncunun cihazında hesaplanıyor. Online sürümde kupa hesabı hile yapılamaması için sunucuya (Firebase Cloud Functions veya oyun sunucusu) taşınacak.

## 13. Altın, garaj, görevler, sıralama

**Altın kazanma:** galibiyet 100, beraberlik 60, yenilgi 40; her öldürme +10, her asist +5, MVP +25. Görevler ve sezon ödülleri de altın verir.

**Garaj → geliştirme** (her sınıf için ayrı, 5 seviye; fiyatlar 200 / 400 / 700 / 1100 / 1600):

| Geliştirme | Seviye başına |
|---|---|
| Motor | +%4 hız |
| Zırh | +%5 can |
| Top | +%4 hasar, -%3 dolum süresi |

Botlar dengeli kalsın diye senin ortalama geliştirme seviyene yakın (±1) seviyelerle doğar.

**Garaj → kamuflaj:** Standart (ücretsiz), Orman ve Çöl (300), Kış (400), Şehir (500), Gece (700), Kızıl (900), Altın (2500). Seçilen kamuflaj sadece senin tankının gövdesine uygulanır; takımı belli eden renkli işaretler değişmez.

**Görevler:** her gün 3 günlük, her pazartesi 3 haftalık görev (Türkiye saati). Maç oyna, kazan, imha et, asist yap, hasar ver, yetenek kullan, güçlendirme topla. Tamamlanan görevin ödülü "Ödülü al" ile alınır.

**Sezon ödülü:** sezon bitince bitirdiğin lige göre: Bronz 200, Gümüş 400, Altın 700, Platin 1000, Elmas 1500, Usta 2500 altın.

**Sıralama:** Firestore kuruluysa en yüksek kupalı 50 oyuncu listelenir (yukarıdaki kuralları güncellemeyi unutma).

Fiyat ve ödülleri değiştirmek için `Scripts/Meta/Economy.cs` dosyasını düzenle.

## 14. Gerçek 3D tank modeli takmak

1. Asset Store'dan bir tank modeli indir ve projeye aktar (Window → Package Manager → My Assets). "Low poly tank" araması iyi başlangıçtır; mobil için düşük poligonlu olanları seç.
2. `Assets/MiniTank/Generated/Classes` içinde modeli kullanmak istediğin sınıfa tıkla (ör. `Agir`).
3. Inspector'da **Özel 3D model** bölümüne modelin prefab'ını sürükle.
   - Model yanlış yöne bakıyorsa **Custom Model Rotation**'ı ayarla (çoğu zaman Y = 90 veya 180).
   - Çok büyük/küçükse **Custom Model Scale**'i değiştir. Kodla üretilen orta tank yaklaşık 4 m uzunluğundadır.
4. **MiniTank → Tankları Yeniden Kur (model değiştirince)**.

Modeldeki parçaların adında şu kelimeler varsa otomatik bulunur: taret için `Turret`, namlu için `Gun`, `Barrel` veya `Cannon`, namlu ağzı için `Muzzle` veya `FirePoint`. Bulunamazsa Console'da uyarı çıkar ve tank çalışır ama o parça görsel olarak dönmez; modelin Hierarchy'sinde parçanın adını değiştirip tekrar kurman yeter. Takımı belli etmek için modele renkli bayraklı bir anten eklenir. Modeli kaldırmak için alanı boşaltıp tekrar kur.

## 15. Online 5v5 (Photon Fusion 2)

**Kurulum (bir kez):**

1. Photon hesabı + Fusion 2 App ID + SDK içe aktarma (yapıldı ✔).
2. Unity'de **MiniTank → Online Kurulumu (Photon)** menüsünü çalıştır. `Assets/MiniTank/Resources/NetMatch.prefab` oluşur. ("Her Şeyi Kur" da bunu otomatik yapar.)
3. Console'da kırmızı hata olmadığını kontrol et.

**Nasıl çalışır:**

- Ana menüde sağ altta **Botlar / Çevrimiçi** seçimi var. Çevrimiçi'yi seç, mod + tank seç, arenaya tıkla.
- Aynı **mod + arenayı** seçen oyuncular aynı odaya düşer. Oda yoksa sen **host** olursun.
- 20 saniye oyuncu beklenir (ekranın üstünde sayaç). Dolmayan yerleri **botlar** doldurur. 10 gerçek oyuncu olursa hemen başlar.
- Maç sırasında gelen oyuncu bir botun yerini alır. Çıkan oyuncunun tankını bot devralır. Son Tank modunda maç başlayınca yeni oyuncu alınmaz.
- Oyunu **host** simüle eder: hareket, mermi, hasar, zırh, yetenekler, güçlendirmeler, skor, süre. Diğerleri girdi gönderir, durumu akıcı gösterir.
- Kendi tankının kulesi gecikmesiz döner. Gövde hareketinde ping kadar gecikme hissedilebilir.
- Lig kupası ve altın maç sonunda herkesin kendi hesabına yazılır. Maçtan çıkan yenilmiş sayılır. Bağlantı koparsa ya da host çıkarsa ceza yoktur ve maç biter.

**Test (tek bilgisayarda iki oyuncu):**

1. **File → Build Profiles**'da Windows'u seç ve **Build** ile bir Windows .exe al (Android'e geri dönmeyi unutma). Ya da telefonuna Android build yükle.
2. Build'i aç: giriş yap → Çevrimiçi → mod + arena seç.
3. Aynı anda Unity editöründe Play'e bas → aynı mod + arenayı seç. İkiniz aynı maçta olmalısınız.

**Bilinen sınırlar:**

- Hileye karşı tam koruma için ayrı bir sunucu (Dedicated Server) ya da bulut fonksiyonu gerekir. Şu an host bir oyuncunun cihazı. Lig/altın hesabı her oyuncunun kendi cihazında yapılır.
- Host oyundan çıkarsa maç biter. Host devri (host migration) yok.
- Ücretsiz Photon planında eşzamanlı kullanıcı (CCU) sınırı var. Geliştirme için yeterli; güncel limit Photon panelinde yazar.

## 16. Reklam ve satın alma

Şu an **test modundadır**:
- Maç sonunda **"Reklam izle: altını 2 katına çıkar"** butonu editörde 2 saniyelik sahte reklam gösterip ödülü verir. Telefonda reklam servisi bağlanana kadar görünmez.
- **Mağaza**'daki altın paketleri editörde ücretsiz test satın almasıdır, gerçek ödeme alınmaz. Telefonda "Mağaza henüz açık değil" der.

Gerçeğe geçmek için (yayına yakın): Google Play Console'da geliştirici hesabı, reklam için Unity LevelPlay veya Google AdMob hesabı, satın alma için Unity IAP paketi ve Play Console'da `minitank.gold.small`, `minitank.gold.medium`, `minitank.gold.large` kimlikli ürünler. Kod tarafında sadece `Scripts/Monetization/Monetization.cs` içindeki test servislerinin yerine gerçekleri konacak; oyunun geri kalanı değişmeyecek.

## 17. Arkadaşla oynama, yeni menü, eğitim ve yeni tank modelleri

**Arkadaşla oynama (oda kodu):**

- Ana menüde **Rakipler → Arkadaşla** seç, mod ve arenayı seç, **ODA KUR**'a bas. Ekranda 5 karakterlik bir kod çıkar (ör. `B7KQ4`).
- Arkadaşın aynı ekranda **Koda katıl** kutusuna kodu yazıp **Katıl**'a basar. Kodun ilk harfi mod + arenayı taşıdığı için arkadaşının ayrıca seçim yapmasına gerek yok.
- Oda sahibi hazır olunca **MAÇI BAŞLAT**'a basar. Boş yerleri botlar doldurur. Başlatılmazsa 5 dakika sonra kendiliğinden başlar.

**Yeni menü:** Arkada döner bir platformda seçili tank (parmakla çevrilebilir), solda mod ve rakip seçimi, ortada tank sınıfı ve özellik çubukları, sağda arena kartları ve **SAVAŞA GİR**. Paneller kayarak açılır, butonlar dokununca büyüyüp küçülür. Garaj, görevler, ayarlar ve maç içi menüler de aynı temayı kullanır.

**Eğitim:** İlk girişte kısa eğitim önerilir (menüdeki **Eğitim** butonuyla tekrar oynanabilir). Sürme, nişan, ateş, zırh, yetenek ve güçlendirme adım adım öğretilir; ilgili buton parlar. Bitirince +200 altın.

**Yeni tank modelleri:** Tek tek palet baklaları, üst makaralar, dişli çark, bölümlü yan etekler, ek zırh blokları, nişangah ve periskoplar, depo sepeti, ısı kılıflı namlu, yıpranmış boya dokusu ve normal haritası. Görmek için **MiniTank → Her Şeyi Kur** çalıştır.

Hazır bir 3D tank modeli kullanmak istersen bölüm 14'teki adımlarla FBX/GLB modelini takabilirsin. Ücretsiz modeller için Unity Asset Store'da "tank" araması, CGTrader veya Free3D'nin ücretsiz low-poly tank modelleri iyi başlangıç noktalarıdır (lisansını kontrol et).
