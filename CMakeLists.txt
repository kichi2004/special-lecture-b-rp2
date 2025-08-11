cmake_minimum_required(VERSION 4.0)
project(final_c C)

set(CMAKE_C_STANDARD 11)

add_executable(final_c
        libjitshim.c)
add_executable(test test.c)
