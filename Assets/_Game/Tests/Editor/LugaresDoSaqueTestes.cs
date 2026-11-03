using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieCityCleaner.Mundo;

namespace ZombieCityCleaner.Testes
{
    /// <summary>
    /// Os lugares do saque nos móveis (o Plano_Saque.md da pasta de arte, a parte 2): a tabela escrita pela montagem da
    /// mobília nova (<see cref="LugaresDoSaque"/>). Todo móvel da tabela tem lugares; cada lugar fica dentro da caixa do
    /// móvel, tem o tamanho mínimo e não passa da altura da mão no jogo (contando a dos pendurados); a parte de cada lugar
    /// de dentro existe no prefab e abre, e só a gaveta e a porta levam o lugar com elas.
    /// </summary>
    public class LugaresDoSaqueTestes
    {
        const string Prefabs = "Assets/_Game/Prefabs/MobiliaNova";

        [Test]
        public void CadaLugarFicaNoMovel()
        {
            Assert.Greater(LugaresDoSaque.Modelos, 40, "a tabela dos lugares está quase vazia (Mobília nova > Escrever os lugares do saque)");
            var erros = new List<string>();
            foreach (var modelo in LugaresDoSaque.Nomes)
            {
                var lugares = LugaresDoSaque.Do(modelo);
                if (lugares.Length == 0) { erros.Add($"{modelo}: nenhum lugar"); continue; }
                if (!MedidasDaMobilia.Tem(modelo)) { erros.Add($"{modelo}: sem as medidas"); continue; }
                var tam = MedidasDaMobilia.Real(modelo);
                var meio = MedidasDaMobilia.Meio(modelo);
                float baixo = MedidasDaMobilia.Baixo(modelo);
                var t = CatalogoDaMobilia.DoModelo(modelo);
                float pendurado = t != null && t.lugar == CatalogoDaMobilia.Lugar.Alto ? t.altura : 0f;
                for (int i = 0; i < lugares.Length; i++)
                {
                    var l = lugares[i];
                    string qual = $"{modelo}, o lugar {i + 1}";
                    if (Mathf.Abs(l.meio.x - meio.x) + l.tamanho.x / 2f > tam.x / 2f + 0.01f || Mathf.Abs(l.meio.z - meio.y) + l.tamanho.z / 2f > tam.z / 2f + 0.01f)
                        erros.Add($"{qual}: sai da caixa do móvel em x ou z");
                    if (l.meio.y < baixo - 0.01f || l.meio.y > baixo + tam.y)
                        erros.Add($"{qual}: a superfície ({l.meio.y:0.00} m) fora da altura do móvel");
                    if (Mathf.Min(l.tamanho.x, l.tamanho.z) < 0.079f || Mathf.Max(l.tamanho.x, l.tamanho.z) < 0.099f || l.tamanho.y < 0.079f)
                        erros.Add($"{qual}: pequeno demais ({l.tamanho.x:0.00} × {l.tamanho.z:0.00} × {l.tamanho.y:0.00} m)");
                    if ((pendurado + l.meio.y) * MedidasDaMobilia.Escala > 1.95f)
                        erros.Add($"{qual}: alto demais no jogo ({(pendurado + l.meio.y) * MedidasDaMobilia.Escala:0.00} m)");
                }
            }
            Assert.IsEmpty(erros, string.Join("\n", erros));
        }

        [Test]
        public void AsPartesDosLugaresAbrem()
        {
            var erros = new List<string>();
            foreach (var modelo in LugaresDoSaque.Nomes)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{modelo}.prefab");
                if (prefab == null) { erros.Add($"{modelo}: sem o prefab"); continue; }
                foreach (var l in LugaresDoSaque.Do(modelo))
                {
                    if (l.parte == null) { if (l.naParte) erros.Add($"{modelo}: um lugar na parte sem a parte"); continue; }
                    var p = prefab.transform.Find($"{modelo}.{l.parte}");
                    var abre = p != null ? p.GetComponent<ParteQueAbre>() : null;
                    if (abre == null) { erros.Add($"{modelo}: a parte {l.parte} não existe ou não abre"); continue; }
                    if (l.naParte && abre.tipo == ParteQueAbre.Tipo.Tampa) erros.Add($"{modelo}: um lugar em cima da tampa {l.parte} (cairia quando ela abre)");
                }
            }
            Assert.IsEmpty(erros, string.Join("\n", erros));
        }
    }
}
