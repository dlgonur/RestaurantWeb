-- ============================================================
-- RestaurantWeb - Database Schema (portable)
-- PostgreSQL
-- ============================================================

-- -------------------------
-- 1) kategoriler
-- -------------------------
CREATE TABLE IF NOT EXISTS public.kategoriler
(
    id              SERIAL PRIMARY KEY,
    ad              VARCHAR(100) NOT NULL UNIQUE,
    aktif_mi        BOOLEAN NOT NULL DEFAULT TRUE,
    olusturma_tarihi TIMESTAMP NOT NULL DEFAULT NOW()
);

-- -------------------------
-- 2) masalar
-- -------------------------
CREATE TABLE IF NOT EXISTS public.masalar
(
    id          SERIAL PRIMARY KEY,
    masa_no     INT NOT NULL UNIQUE,
    kapasite    INT NOT NULL DEFAULT 4,
    aktif_mi    BOOLEAN NOT NULL DEFAULT TRUE,
    durum       SMALLINT NOT NULL DEFAULT 0,
    CONSTRAINT ck_masalar_durum CHECK (durum IN (0, 1, 2))
);

-- -------------------------
-- 3) personeller
-- -------------------------
CREATE TABLE IF NOT EXISTS public.personeller
(
    id              SERIAL PRIMARY KEY,
    ad_soyad        VARCHAR(150) NOT NULL,
    kullanici_adi   VARCHAR(100) NOT NULL UNIQUE,
    sifre_hash      TEXT NOT NULL,
    sifre_salt      TEXT NOT NULL,
    rol             INTEGER NOT NULL,
    aktif_mi        BOOLEAN NOT NULL DEFAULT TRUE,
    olusturma_tarihi TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_personeller_rol_nonzero CHECK (rol <> 0)
);

CREATE INDEX IF NOT EXISTS ix_personeller_kullanici_adi
    ON public.personeller (kullanici_adi);

-- -------------------------
-- 4) urunler
-- -------------------------
CREATE TABLE IF NOT EXISTS public.urunler
(
    id              SERIAL PRIMARY KEY,
    kategori_id     INT NOT NULL,
    ad              VARCHAR(150) NOT NULL,
    fiyat           NUMERIC(10,2) NOT NULL,
    aktif_mi        BOOLEAN NOT NULL DEFAULT TRUE,
    olusturma_tarihi TIMESTAMP NOT NULL DEFAULT NOW(),
    stok            INT NOT NULL DEFAULT 0,
    resim           BYTEA,
    resim_mime      VARCHAR(100),
    resim_adi       VARCHAR(255),

    CONSTRAINT uq_urunler_kategori_ad UNIQUE (kategori_id, ad),
    CONSTRAINT fk_urunler_kategori FOREIGN KEY (kategori_id)
        REFERENCES public.kategoriler (id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT ck_urunler_fiyat_nonnegative CHECK (fiyat >= 0),
    CONSTRAINT ck_urunler_stok_nonnegative CHECK (stok >= 0)
);

CREATE INDEX IF NOT EXISTS idx_urunler_kategori_id
    ON public.urunler (kategori_id);

-- -------------------------
-- 5) siparisler
-- -------------------------
CREATE TABLE IF NOT EXISTS public.siparisler
(
    id                  SERIAL PRIMARY KEY,
    masa_id              INT NOT NULL,
    durum               SMALLINT NOT NULL DEFAULT 0,
    ara_toplam          NUMERIC(10,2) NOT NULL DEFAULT 0,
    iskonto             NUMERIC(10,2) NOT NULL DEFAULT 0,
    toplam              NUMERIC(10,2) NOT NULL DEFAULT 0,
    olusturma_tarihi     TIMESTAMP NOT NULL DEFAULT NOW(),
    kapatma_tarihi       TIMESTAMP,
    iskonto_oran         NUMERIC(5,2) NOT NULL DEFAULT 0,
    iskonto_tutar        NUMERIC(12,2) NOT NULL DEFAULT 0,
    kapandi_tarihi       TIMESTAMP,
    kapatan_personel_id  INT,

    CONSTRAINT fk_siparisler_masa FOREIGN KEY (masa_id)
        REFERENCES public.masalar (id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_siparis_kapatan_personel FOREIGN KEY (kapatan_personel_id)
        REFERENCES public.personeller (id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CONSTRAINT ck_siparisler_durum CHECK (durum IN (0, 1, 2)),
    CONSTRAINT ck_siparisler_tutarlar CHECK (ara_toplam >= 0 AND iskonto >= 0 AND toplam >= 0)
);

CREATE INDEX IF NOT EXISTS ix_siparisler_kapatan_personel
    ON public.siparisler (kapatan_personel_id);

-- tek açık sipariş (durum=0) kuralı
CREATE UNIQUE INDEX IF NOT EXISTS ux_siparisler_masa_acik
    ON public.siparisler (masa_id)
    WHERE durum = 0;

-- -------------------------
-- 6) siparis_kalemleri
-- -------------------------
CREATE TABLE IF NOT EXISTS public.siparis_kalemleri
(
    id          SERIAL PRIMARY KEY,
    siparis_id  INT NOT NULL,
    urun_id     INT NOT NULL,
    adet        INT NOT NULL,
    birim_fiyat NUMERIC(10,2) NOT NULL,
    satir_toplam NUMERIC(10,2) NOT NULL,
    durum       SMALLINT NOT NULL DEFAULT 0,

    CONSTRAINT fk_kalem_siparis FOREIGN KEY (siparis_id)
        REFERENCES public.siparisler (id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    CONSTRAINT fk_kalem_urun FOREIGN KEY (urun_id)
        REFERENCES public.urunler (id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT ck_kalem_adet CHECK (adet > 0),
    CONSTRAINT ck_kalem_tutarlar CHECK (birim_fiyat >= 0 AND satir_toplam >= 0),
    CONSTRAINT ck_kalem_durum CHECK (durum IN (0, 1, 2, 3))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_siparis_kalemleri_siparis_urun
    ON public.siparis_kalemleri (siparis_id, urun_id);

-- -------------------------
-- 7) odemeler
-- -------------------------
CREATE TABLE IF NOT EXISTS public.odemeler
(
    id              SERIAL PRIMARY KEY,
    siparis_id      INT NOT NULL UNIQUE,
    tutar           NUMERIC(12,2) NOT NULL,
    yontem          SMALLINT NOT NULL,
    alindi_tarihi   TIMESTAMP NOT NULL DEFAULT NOW(),
    aciklama        VARCHAR(200),

    CONSTRAINT odemeler_siparis_id_fkey FOREIGN KEY (siparis_id)
        REFERENCES public.siparisler (id)
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,

    CONSTRAINT ck_odemeler_tutar CHECK (tutar >= 0)
);

CREATE INDEX IF NOT EXISTS ix_odemeler_tarih
    ON public.odemeler (alindi_tarihi);

-- -------------------------
-- 8) siparis_log
-- -------------------------
CREATE TABLE IF NOT EXISTS public.siparis_log
(
    id              SERIAL PRIMARY KEY,
    siparis_id      INT NOT NULL,
    action          VARCHAR(50) NOT NULL,
    old_value       VARCHAR(100),
    new_value       VARCHAR(100),
    actor_username  VARCHAR(100),
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT siparis_log_siparis_id_fkey FOREIGN KEY (siparis_id)
        REFERENCES public.siparisler (id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_siparis_log_siparis_id
    ON public.siparis_log (siparis_id);

CREATE INDEX IF NOT EXISTS ix_siparis_log_created_at
    ON public.siparis_log (created_at);

-- -------------------------
-- 9) personel_loglari
-- -------------------------
CREATE TABLE IF NOT EXISTS public.personel_loglari
(
    id                  SERIAL PRIMARY KEY,
    actor_personel_id    INT,
    actor_kullanici_adi  VARCHAR(100),
    target_personel_id   INT,
    target_kullanici_adi VARCHAR(100),
    aksiyon              VARCHAR(50) NOT NULL,
    old_rol              INT,
    new_rol              INT,
    old_aktif_mi         BOOLEAN,
    new_aktif_mi         BOOLEAN,
    aciklama             TEXT,
    ip                   VARCHAR(64),
    created_at           TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_personel_loglari_actor
    ON public.personel_loglari (actor_personel_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_personel_loglari_target
    ON public.personel_loglari (target_personel_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_personel_loglari_aksiyon
    ON public.personel_loglari (aksiyon);

CREATE INDEX IF NOT EXISTS ix_personel_loglari_created_at
    ON public.personel_loglari (created_at DESC);

-- -------------------------
-- 10) rezervasyonlar
-- -------------------------
CREATE TABLE IF NOT EXISTS public.rezervasyonlar
(
    id                  SERIAL PRIMARY KEY,
    masa_id              INT NOT NULL,
    musteri_ad           VARCHAR(150) NOT NULL,
    telefon              VARCHAR(30) NOT NULL,
    rezervasyon_tarihi   TIMESTAMP NOT NULL,
    kisi_sayisi          INT,
    notlar               TEXT,
    durum               SMALLINT NOT NULL DEFAULT 0,
    olusturma_tarihi     TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_rezervasyonlar_masa FOREIGN KEY (masa_id)
        REFERENCES public.masalar (id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT ck_rezervasyonlar_durum CHECK (durum IN (0, 1, 2))
);

CREATE INDEX IF NOT EXISTS ix_rez_aktif_tarih
    ON public.rezervasyonlar (durum, rezervasyon_tarihi);

CREATE INDEX IF NOT EXISTS ix_rez_masa_aktif_tarih
    ON public.rezervasyonlar (masa_id, durum, rezervasyon_tarihi);

-- aynı masa + aynı tarih için sadece aktif (durum=0) tek rezervasyon
CREATE UNIQUE INDEX IF NOT EXISTS ux_rez_masa_tarih_aktif
    ON public.rezervasyonlar (masa_id, rezervasyon_tarihi)
    WHERE durum = 0;
