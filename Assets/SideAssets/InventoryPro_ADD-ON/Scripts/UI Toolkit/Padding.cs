namespace cowsins.Inventory
{
    [System.Serializable]
    public class Padding
    {
        public float horizontal;
        public float vertical;

        public Padding(float x = 0, float y = 0)
        {
            this.horizontal = x;
            this.vertical = y;
        }
    }
}