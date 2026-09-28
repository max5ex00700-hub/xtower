cmake_minimum_required(VERSION 3.22.1)
project(thumbrhythm_native_audio)

include(FetchContent)

set(OBOE_ENABLE_TESTS OFF CACHE BOOL "" FORCE)
set(OBOE_ENABLE_EXAMPLES OFF CACHE BOOL "" FORCE)
FetchContent_Declare(
    oboe
    GIT_REPOSITORY https://github.com/google/oboe.git
    GIT_TAG 1.11.0
    GIT_SHALLOW TRUE
)
FetchContent_MakeAvailable(oboe)

FetchContent_Declare(
    miniaudio
    GIT_REPOSITORY https://github.com/mackron/miniaudio.git
    GIT_TAG 0.11.25
    GIT_SHALLOW TRUE
)
FetchContent_GetProperties(miniaudio)
if(NOT miniaudio_POPULATED)
    FetchContent_Populate(miniaudio)
endif()

add_library(native-audio SHARED NativeAudioEngine.cpp)
target_include_directories(native-audio PRIVATE ${miniaudio_SOURCE_DIR})
target_link_libraries(native-audio PRIVATE oboe log android)
