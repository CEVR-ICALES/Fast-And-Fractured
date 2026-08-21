using UnityEngine;

namespace FastAndFractured.Abstractions
{
    /// <summary>
    /// Define un contrato para cualquier componente capaz de iniciar las acciones de disparo.
    /// Esto desacopla la maquina de estados de la implementacion de red o local.
    /// </summary>
    public interface IShootController
    {
        void TryNormalShoot();
        void TryPushShoot();
        void TryMineShoot();
    }
}
