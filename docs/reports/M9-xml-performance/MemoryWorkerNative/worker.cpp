#define main performance_worker_main
#include "../NativeWorker/worker.cpp"
#undef main
#include <cstdlib>
#include <cstddef>
#include <limits>

// Instrumented sidecar only: timing uses the original uninstrumented worker.
// Counters measure requested payload bytes; allocator metadata is excluded.
struct alignas(std::max_align_t) Header { size_t size; };
static uint64_t live_bytes = 0, allocated_bytes = 0, peak_bytes = 0;
static void* counted_malloc(size_t size) {
    if (size > std::numeric_limits<size_t>::max() - sizeof(Header)) return nullptr;
    auto h = static_cast<Header*>(std::malloc(sizeof(Header) + size));
    if (!h) return nullptr;
    h->size = size; live_bytes += size; allocated_bytes += size; peak_bytes = std::max(peak_bytes, live_bytes);
    return h + 1;
}
static void counted_free(void* value) {
    if (!value) return;
    auto h = static_cast<Header*>(value) - 1; live_bytes -= h->size; std::free(h);
}
static void* counted_realloc(void* value, size_t size) {
    if (!value) return counted_malloc(size);
    if (!size) { counted_free(value); return nullptr; }
    if (size > std::numeric_limits<size_t>::max() - sizeof(Header)) return nullptr;
    auto old = static_cast<Header*>(value) - 1; size_t previous = old->size;
    auto h = static_cast<Header*>(std::realloc(old, sizeof(Header) + size));
    if (!h) return nullptr;
    h->size = size; live_bytes = live_bytes - previous + size; allocated_bytes += size; peak_bytes = std::max(peak_bytes, live_bytes);
    return h + 1;
}
static char* counted_strdup(const char* value) {
    size_t size = std::strlen(value) + 1; auto result = static_cast<char*>(counted_malloc(size));
    if (result) std::memcpy(result, value, size); return result;
}
int main(int argc, char** argv) {
    try {
        if (argc != 2) throw std::runtime_error("fixture required");
        if (xmlMemSetup(counted_free, counted_malloc, counted_realloc, counted_strdup)) throw std::runtime_error("allocator installation failed");
        for (size_t size : {32768U, 9437184U}) {
            auto baseline = live_bytes; auto value = xmlMalloc(size);
            if (!value || live_bytes - baseline != size) throw std::runtime_error("allocation control failed");
            xmlFree(value); if (live_bytes != baseline) throw std::runtime_error("release control failed");
        }
        std::ifstream input(argv[1], std::ios::binary);
        std::string bytes((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
        if (!input || bytes.empty()) throw std::runtime_error("fixture read failed");
        auto warm = parse(bytes); xmlFreeDoc(warm);
        std::cout << "{\"engine\":\"libxml2-2.15.4\",\"payloadControlsPassed\":true,\"rows\":[";
        for (int cycle = 0; cycle < 3; cycle++) {
            uint64_t baseline = live_bytes, start = allocated_bytes; peak_bytes = baseline;
            auto document = parse(bytes);
            uint64_t retained = live_bytes - baseline, allocated = allocated_bytes - start, peak = peak_bytes - baseline;
            xmlFreeDoc(document);
            if (cycle) std::cout << ',';
            std::cout << "{\"cycle\":" << cycle << ",\"allocatedBytes\":" << allocated << ",\"retainedBytes\":" << retained
                      << ",\"peakBytes\":" << peak << ",\"releasedDelta\":" << static_cast<int64_t>(live_bytes - baseline) << '}';
        }
        std::cout << "]}\n"; return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
