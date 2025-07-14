using UnityEngine;

namespace DestroyIt
{
    public class RocketLoading : MonoBehaviour
    {
        public bool isLoaded = true; // start off with the rocket loaded into the launcher

        private void OnEnable()
        {
            if (isLoaded) // if the rocket is already loaded, position the game object so it is in the launcher.
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0.74f);
            else // if rocket is not currently loaded, play the rocket loading animation
            {
                Animation anim = gameObject.GetComponent<Animation>();
                if (anim != null)
                    anim.Play("Rocket Loading");
                isLoaded = true;
            }
        }
    }
}