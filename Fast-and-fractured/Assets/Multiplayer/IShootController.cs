using UnityEngine;

namespace FastAndFractured.Abstractions
{
    /// <summary>
    /// Define un contrato para cualquier componente capaz de iniciar las acciones de disparo.
    /// Esto desacopla la máquina de estados de la implementación de red o local.
    /// </summary>
    public interface IShootController
    {
        void TryNormalShoot();
        void TryPushShoot();
        void TryMineShoot();
    }
}