#include <sys/mman.h>
#include <unistd.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/errno.h>

void __clear_cache(void *start, void *end);

size_t get_page_size() {
    return (size_t) sysconf(_SC_PAGESIZE);
}

void *jit_alloc(const size_t size) {
    const size_t page_size = get_page_size();
    const size_t alloc = (size + page_size - 1) & ~(page_size - 1);
    void *ptr = mmap(NULL, alloc, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

    if (ptr == MAP_FAILED) {
        return NULL;
    }
    return ptr;
}

int jit_make_executable(void *start, const size_t size) {
    size_t page_size = get_page_size();
    size_t len = (size + page_size - 1) & ~(page_size - 1);

    __clear_cache(start, start + len);
    return mprotect(start, len, PROT_READ | PROT_EXEC);
}

int jit_free(void *start, const size_t size) {
    size_t page_size = get_page_size();
    size_t len = (size + page_size - 1) & ~(page_size - 1);
    return munmap(start, len);
}

int add(const int a, const int b)
{
    return a + b;
}

int get_errno() {
    return errno;
}
