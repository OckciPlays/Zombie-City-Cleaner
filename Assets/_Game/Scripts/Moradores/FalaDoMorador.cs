using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Moradores
{
    /// <summary>
    /// O que a conversa com um morador precisa saber dele, em todas as máquinas (25/11/2026, a etapa 8, a pedido: "os
    /// moradores conversando"): o nome completo, a idade, o que ele fazia antes, o que viveu nos 5 anos, o cargo, a função
    /// (a habilidade mais forte) e como ele está (o humor, a fome, a sede, o sono e a crise). O anfitrião escreve
    /// (<see cref="MoradoresDoBunker"/>.Conversa.cs) a cada poucos segundos; a ficha inteira fica só nele.
    /// </summary>
    public struct FalaDoMorador : INetworkSerializable, IEquatable<FalaDoMorador>
    {
        public FixedString64Bytes nomeCompleto, antes, depoisChave;
        public FixedString32Bytes cargo;
        public byte idade, funcao, humor, fome, sede, sono, crise, atividade;
        public bool mulher, ygayr;
        /// <summary>
        /// Deitado dormindo, ou cochilando no sofá (25/11/2026, a revisão: o morador do beliche respondia "Ei! Bom te ver por
        /// aqui." de olhos fechados; a conversa dele agora é a de quem foi acordado).
        /// </summary>
        public bool dormindo;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref nomeCompleto);
            s.SerializeValue(ref antes);
            s.SerializeValue(ref depoisChave);
            s.SerializeValue(ref cargo);
            s.SerializeValue(ref idade);
            s.SerializeValue(ref funcao);
            s.SerializeValue(ref humor);
            s.SerializeValue(ref fome);
            s.SerializeValue(ref sede);
            s.SerializeValue(ref sono);
            s.SerializeValue(ref crise);
            s.SerializeValue(ref atividade);
            s.SerializeValue(ref mulher);
            s.SerializeValue(ref ygayr);
            s.SerializeValue(ref dormindo);
        }

        public bool Equals(FalaDoMorador o) =>
            nomeCompleto.Equals(o.nomeCompleto) && antes.Equals(o.antes) && depoisChave.Equals(o.depoisChave) && cargo.Equals(o.cargo)
            && idade == o.idade && funcao == o.funcao && humor == o.humor && fome == o.fome && sede == o.sede && sono == o.sono
            && crise == o.crise && atividade == o.atividade && mulher == o.mulher && ygayr == o.ygayr && dormindo == o.dormindo;

        /// <summary>Já tem o que mostrar (o anfitrião já escreveu).</summary>
        public bool Pronta => nomeCompleto.Length > 0;

        /// <summary>
        /// No anfitrião: ele está dormindo agora (no lugar, deitado; ou no sofá, na hora de dormir ou do cochilo). Quem ainda vai
        /// para a cama, ou já levantou, está acordado.
        /// </summary>
        public static bool Dormindo(VidaDoMorador v)
        {
            if (v == null || !v.noLugar || v.lugar < 0) return false;
            var acao = AcoesDoCorpo.Acao(v.acao);
            var at = v.Atividade;
            return acao == AcaoDoCorpo.Dormindo
                   || (acao == AcaoDoCorpo.Sofa && (at == AtividadeDoMorador.Dormir || at == AtividadeDoMorador.Cochilo));
        }

        /// <summary>No anfitrião: a fala pela ficha e pela vida de agora.</summary>
        public static FalaDoMorador Da(FichaDoMorador f, VidaDoMorador v)
        {
            if (f == null) return default;
            var antes = ListasDosMoradores.DeAntes(f.antes);
            string oficio = antes == null ? "" : (f.mulher ? antes.nomeF : antes.nomeM);
            int melhor = 0;
            for (int h = 1; h < f.habilidades.Length; h++)
                if (f.habilidades[h] > f.habilidades[melhor]) melhor = h;
            return new FalaDoMorador
            {
                nomeCompleto = new FixedString64Bytes(Curto(f.NomeCompleto, 58)),
                antes = new FixedString64Bytes(Curto(oficio, 58)),
                depoisChave = new FixedString64Bytes(Curto(f.depois ?? "", 58)),
                cargo = new FixedString32Bytes(Curto(f.cargo ?? "", 28)),
                idade = (byte)Mathf.Clamp(f.idade, 0, 255),
                funcao = (byte)melhor,
                humor = (byte)Mathf.Clamp(Mathf.RoundToInt(v != null ? v.humor : 50f), 0, 100),
                fome = (byte)Mathf.Clamp(Mathf.RoundToInt(v != null ? v.fome : 75f), 0, 100),
                sede = (byte)Mathf.Clamp(Mathf.RoundToInt(v != null ? v.sede : 75f), 0, 100),
                sono = (byte)Mathf.Clamp(Mathf.RoundToInt(v != null ? v.sono : 70f), 0, 100),
                crise = v != null ? v.crise : (byte)0,
                atividade = v != null ? v.atividade : (byte)0,
                mulher = f.mulher,
                ygayr = MoradoresDoBunker.EhOYgayr(f),
                dormindo = Dormindo(v),
            };
        }

        static string Curto(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // (o FixedString64Bytes guarda 61 bytes: os acentos ocupam 2)
            while (System.Text.Encoding.UTF8.GetByteCount(s) > n) s = s.Substring(0, s.Length - 1);
            return s;
        }
    }
}
