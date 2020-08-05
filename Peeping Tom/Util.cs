namespace PeepingTom {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
    public class Optional<T> {
        public bool Present { get; private set; }
        private readonly T value;

        public Optional(T value) {
            this.value = value;
            this.Present = true;
        }

        public Optional() {
            this.Present = false;
        }

        public bool Get(out T value) {
            value = this.value;
            return this.Present;
        }
    }
}
