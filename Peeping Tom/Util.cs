namespace PeepingTom {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")]
    public class Optional<T> where T : class {
        public bool Present { get; }
        private readonly T? _value;

        public Optional(T? value) {
            this._value = value;
            this.Present = true;
        }

        public Optional() {
            this.Present = false;
        }

        public bool Get(out T? value) {
            value = this._value;
            return this.Present;
        }
    }
}
