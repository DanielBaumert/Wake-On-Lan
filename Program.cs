namespace Se7en.MagicWakeOnLan
{
    class Program
    {
        static void Main(string[] args)
        {
            using (WakeOnLan wakeOnLan = new WakeOnLan())
            {
                wakeOnLan.Run();
            }
        }
       
    }
}
