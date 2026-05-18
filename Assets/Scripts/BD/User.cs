using System;

[Serializable]
public class User
{
    public int id_usuari;
    public string nom;

    public string tipus_sala;
    public string postura_inicial;

    public bool independent;
    public bool entorn_adult;

    public bool menu_mans_actiu;
    public bool particules_mans_actives;

    public bool actiu;

    public string creat_a;
    public string actualitzat_a;
}
