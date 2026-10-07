namespace XRUIOS.Barebones
{

    //From https://gist.github.com/bugshake/d4a6ea578f2f96f07c5c6c2d701141e6
    // get an event when a property changes

    public class ObservableProperty<T>
    {
        T value;

        public delegate void ChangeEvent(T data);
        public event ChangeEvent changed;

        public ObservableProperty(T initialValue)
        {
            value = initialValue;
        }

        public void Set(T v)
        {
            if (!v.Equals(value))
            {
                value = v;
                if (changed != null)
                {
                    changed(value);
                }
            }
        }

        public T Get()
        {
            return value;
        }

        public static implicit operator T(ObservableProperty<T> p)
        {
            return p.value;
        }
    }

}
